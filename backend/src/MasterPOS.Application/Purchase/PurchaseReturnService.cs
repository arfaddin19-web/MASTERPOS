using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Inventory;
using MasterPOS.Domain.Masters;
using MasterPOS.Domain.Purchase;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Purchase;

public class PurchaseReturnService : IPurchaseReturnService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public PurchaseReturnService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<PurchaseReturnDto> CreateAsync(CreatePurchaseReturnRequest request, CancellationToken ct = default)
    {
        var branchId = _currentUser.BranchId
            ?? throw new AppException("Your account has no default branch assigned.");

        var supplier = await _db.Parties.SingleOrDefaultAsync(
            p => p.Id == request.SupplierId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct)
            ?? throw new AppException("The selected supplier does not exist.");
        if (supplier.PartyType == PartyType.Customer)
            throw new AppException($"'{supplier.Name}' is set up as a Customer, not a Supplier.");

        if (request.OriginalPurchaseInvoiceId is { } originalId)
        {
            var original = await _db.PurchaseInvoices.SingleOrDefaultAsync(
                i => i.Id == originalId && i.CompanyId == _currentUser.CompanyId && !i.IsDeleted, ct)
                ?? throw new AppException("The referenced purchase invoice does not exist.");
            if (original.Status != DocumentStatus.Posted)
                throw new AppException($"Invoice {original.InvoiceNumber} is {original.Status} — only a Posted invoice can be returned against.");
        }

        var ret = new PurchaseReturn
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = branchId,
            ReturnNumber = await GenerateNumberAsync(ct),
            SupplierId = request.SupplierId,
            OriginalPurchaseInvoiceId = request.OriginalPurchaseInvoiceId,
            ReturnDate = request.ReturnDate,
            Narration = request.Narration,
            Status = DocumentStatus.Draft,
        };
        _db.PurchaseReturns.Add(ret);
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(ret.Id, ct));
    }

    public async Task<PurchaseReturnDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<PurchaseReturnDto>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _db.PurchaseReturns
            .Include(r => r.Supplier)
            .Include(r => r.Lines).ThenInclude(l => l.Product)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .Where(r => r.CompanyId == _currentUser.CompanyId && !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DocumentStatus>(status, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown status '{status}'.");
            query = query.Where(r => r.Status == parsed);
        }

        var returns = await query.OrderByDescending(r => r.ReturnDate).ToListAsync(ct);
        return returns.Select(ToDto).ToList();
    }

    public async Task<PurchaseReturnDto> AddLineAsync(Guid returnId, AddPurchaseReturnLineRequest request, CancellationToken ct = default)
    {
        var ret = await GetOwnedAsync(returnId, ct);
        EnsureDraft(ret);

        var product = await ValidateReturnableProductAsync(request.ProductId, ct);
        await ValidateUnitAsync(request.UnitId, ct);
        ValidateLineNumbers(request.Quantity, request.Rate, request.VatPercent);

        _db.PurchaseReturnLines.Add(new PurchaseReturnLine
        {
            PurchaseReturnId = ret.Id,
            ProductId = product.Id,
            UnitId = request.UnitId,
            Quantity = request.Quantity,
            Rate = request.Rate,
            VatPercent = request.VatPercent,
            LineAmount = ComputeLineAmount(request.Quantity, request.Rate, request.VatPercent),
        });
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(returnId, ct);
        return ToDto(await GetOwnedAsync(returnId, ct));
    }

    public async Task<PurchaseReturnDto> UpdateLineAsync(Guid returnId, Guid lineId, UpdatePurchaseReturnLineRequest request, CancellationToken ct = default)
    {
        var ret = await GetOwnedAsync(returnId, ct);
        EnsureDraft(ret);

        var line = ret.Lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new AppException("Return line not found.");
        await ValidateUnitAsync(request.UnitId, ct);
        ValidateLineNumbers(request.Quantity, request.Rate, request.VatPercent);

        line.UnitId = request.UnitId;
        line.Quantity = request.Quantity;
        line.Rate = request.Rate;
        line.VatPercent = request.VatPercent;
        line.LineAmount = ComputeLineAmount(request.Quantity, request.Rate, request.VatPercent);
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(returnId, ct);
        return ToDto(await GetOwnedAsync(returnId, ct));
    }

    public async Task<PurchaseReturnDto> RemoveLineAsync(Guid returnId, Guid lineId, CancellationToken ct = default)
    {
        var ret = await GetOwnedAsync(returnId, ct);
        EnsureDraft(ret);

        var line = ret.Lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new AppException("Return line not found.");
        _db.PurchaseReturnLines.Remove(line);
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(returnId, ct);
        return ToDto(await GetOwnedAsync(returnId, ct));
    }

    public async Task<PurchaseReturnDto> PostAsync(Guid returnId, CancellationToken ct = default)
    {
        var ret = await GetOwnedAsync(returnId, ct);
        EnsureDraft(ret);
        if (ret.Lines.Count == 0)
            throw new AppException("Add at least one item before posting.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var line in ret.Lines)
        {
            var warehouseId = line.Product.DefaultWarehouseId
                ?? throw new AppException($"'{line.Product.Name}' has no default warehouse — set one before posting this return.");
            _db.StockLedgerEntries.Add(new StockLedgerEntry
            {
                CompanyId = _currentUser.CompanyId,
                WarehouseId = warehouseId,
                ProductId = line.ProductId,
                MovementDate = today,
                QuantityOut = line.Quantity,
                ReferenceType = StockReferenceType.PurchaseReturn,
                ReferenceId = ret.Id,
                CreatedByUserId = _currentUser.UserId,
            });
        }

        ret.Status = DocumentStatus.Posted;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Posted", "Purchase.PurchaseReturns", ret.Id,
            $"posted return {ret.ReturnNumber} (Rs. {ret.GrandTotalAmount:0.00})", ct);
        return ToDto(await GetOwnedAsync(returnId, ct));
    }

    public async Task<PurchaseReturnDto> CancelAsync(Guid returnId, CancellationToken ct = default)
    {
        var ret = await GetOwnedAsync(returnId, ct);
        if (ret.Status != DocumentStatus.Draft)
            throw new AppException($"Return {ret.ReturnNumber} is {ret.Status} and can no longer be cancelled.");

        ret.Status = DocumentStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return ToDto(ret);
    }

    private async Task RecalculateTotalsAsync(Guid returnId, CancellationToken ct)
    {
        var ret = await _db.PurchaseReturns.Include(r => r.Lines).SingleAsync(r => r.Id == returnId, ct);

        var subTotal = ret.Lines.Sum(l => l.Quantity * l.Rate);
        var vat = ret.Lines.Sum(l => l.Quantity * l.Rate * l.VatPercent / 100m);

        ret.SubTotalAmount = Math.Round(subTotal, 2);
        ret.VatAmount = Math.Round(vat, 2);
        ret.GrandTotalAmount = Math.Round(subTotal + vat, 2);
        await _db.SaveChangesAsync(ct);
    }

    private static decimal ComputeLineAmount(decimal quantity, decimal rate, decimal vatPercent)
    {
        var baseAmount = quantity * rate;
        return Math.Round(baseAmount + baseAmount * vatPercent / 100m, 2);
    }

    private static void ValidateLineNumbers(decimal quantity, decimal rate, decimal vatPercent)
    {
        if (quantity <= 0)
            throw new AppException("Quantity must be greater than zero.");
        if (rate < 0)
            throw new AppException("Rate can't be negative.");
        if (vatPercent < 0)
            throw new AppException("Tax % can't be negative.");
    }

    private async Task<Product> ValidateReturnableProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products.SingleOrDefaultAsync(
            p => p.Id == productId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct)
            ?? throw new AppException("The selected product does not exist.");
        if (product.ProductType is ProductType.Recipe or ProductType.Service)
            throw new AppException($"'{product.Name}' is a {product.ProductType} product — it can't be returned to a supplier.");
        return product;
    }

    private async Task ValidateUnitAsync(Guid unitId, CancellationToken ct)
    {
        if (!await _db.Units.AnyAsync(u => u.Id == unitId && u.CompanyId == _currentUser.CompanyId && !u.IsDeleted, ct))
            throw new AppException("The selected unit does not exist.");
    }

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        const string prefix = "PRET-";
        var companyId = _currentUser.CompanyId;
        var numbers = await _db.PurchaseReturns
            .Where(r => r.CompanyId == companyId && r.ReturnNumber.StartsWith(prefix))
            .Select(r => r.ReturnNumber)
            .ToListAsync(ct);

        var next = numbers
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var num) ? num : 0)
            .DefaultIfEmpty(1000)
            .Max() + 1;

        return $"{prefix}{next}";
    }

    private static void EnsureDraft(PurchaseReturn ret)
    {
        if (ret.Status != DocumentStatus.Draft)
            throw new AppException($"Return {ret.ReturnNumber} is {ret.Status} and can no longer be changed.");
    }

    private async Task<PurchaseReturn> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var ret = await _db.PurchaseReturns
            .Include(r => r.Supplier)
            .Include(r => r.Lines).ThenInclude(l => l.Product)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .SingleOrDefaultAsync(r => r.Id == id && r.CompanyId == _currentUser.CompanyId && !r.IsDeleted, ct);
        return ret ?? throw new AppException("Purchase return not found.");
    }

    private static PurchaseReturnDto ToDto(PurchaseReturn r) => new(
        r.Id, r.ReturnNumber, r.SupplierId, r.Supplier.Name, r.OriginalPurchaseInvoiceId,
        r.ReturnDate, r.Status.ToString(),
        r.SubTotalAmount, r.VatAmount, r.GrandTotalAmount, r.Narration,
        r.Lines.Select(l => new PurchaseReturnLineDto(
            l.Id, l.ProductId, l.Product.Name, l.UnitId, l.Unit.Name,
            l.Quantity, l.Rate, l.VatPercent, l.LineAmount)).ToList());
}
