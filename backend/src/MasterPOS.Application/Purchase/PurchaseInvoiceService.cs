using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Inventory;
using MasterPOS.Domain.Masters;
using MasterPOS.Domain.Purchase;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Purchase;

public class PurchaseInvoiceService : IPurchaseInvoiceService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public PurchaseInvoiceService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<PurchaseInvoiceDto> CreateAsync(CreatePurchaseInvoiceRequest request, CancellationToken ct = default)
    {
        var branchId = _currentUser.BranchId
            ?? throw new AppException("Your account has no default branch assigned.");

        var supplier = await _db.Parties.SingleOrDefaultAsync(
            p => p.Id == request.SupplierId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct)
            ?? throw new AppException("The selected supplier does not exist.");
        if (supplier.PartyType == PartyType.Customer)
            throw new AppException($"'{supplier.Name}' is set up as a Customer, not a Supplier.");

        var invoice = new PurchaseInvoice
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = branchId,
            InvoiceNumber = await GenerateNumberAsync("PO-", ct),
            SupplierId = request.SupplierId,
            SupplierReferenceNo = request.SupplierReferenceNo,
            InvoiceDate = request.InvoiceDate,
            PaymentTerms = request.PaymentTerms,
            Narration = request.Narration,
            Status = DocumentStatus.Draft,
        };
        _db.PurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(invoice.Id, ct));
    }

    public async Task<PurchaseInvoiceDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<PurchaseInvoiceDto>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _db.PurchaseInvoices
            .Include(i => i.Supplier)
            .Include(i => i.Lines).ThenInclude(l => l.Product)
            .Include(i => i.Lines).ThenInclude(l => l.Unit)
            .Where(i => i.CompanyId == _currentUser.CompanyId && !i.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DocumentStatus>(status, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown status '{status}'.");
            query = query.Where(i => i.Status == parsed);
        }

        var invoices = await query.OrderByDescending(i => i.InvoiceDate).ToListAsync(ct);
        return invoices.Select(ToDto).ToList();
    }

    public async Task<PurchaseInvoiceDto> AddLineAsync(Guid invoiceId, AddPurchaseInvoiceLineRequest request, CancellationToken ct = default)
    {
        var invoice = await GetOwnedAsync(invoiceId, ct);
        EnsureDraft(invoice);

        var product = await ValidatePurchasableProductAsync(request.ProductId, ct);
        await ValidateUnitAsync(request.UnitId, ct);
        ValidateLineNumbers(request.Quantity, request.Rate, request.DiscountPercent, request.VatPercent);

        _db.PurchaseInvoiceLines.Add(new PurchaseInvoiceLine
        {
            PurchaseInvoiceId = invoice.Id,
            ProductId = product.Id,
            UnitId = request.UnitId,
            Quantity = request.Quantity,
            Rate = request.Rate,
            DiscountPercent = request.DiscountPercent,
            VatPercent = request.VatPercent,
            LineAmount = ComputeLineAmount(request.Quantity, request.Rate, request.DiscountPercent, request.VatPercent),
        });
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(invoiceId, ct);
        return ToDto(await GetOwnedAsync(invoiceId, ct));
    }

    public async Task<PurchaseInvoiceDto> UpdateLineAsync(Guid invoiceId, Guid lineId, UpdatePurchaseInvoiceLineRequest request, CancellationToken ct = default)
    {
        var invoice = await GetOwnedAsync(invoiceId, ct);
        EnsureDraft(invoice);

        var line = invoice.Lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new AppException("Invoice line not found.");
        await ValidateUnitAsync(request.UnitId, ct);
        ValidateLineNumbers(request.Quantity, request.Rate, request.DiscountPercent, request.VatPercent);

        line.UnitId = request.UnitId;
        line.Quantity = request.Quantity;
        line.Rate = request.Rate;
        line.DiscountPercent = request.DiscountPercent;
        line.VatPercent = request.VatPercent;
        line.LineAmount = ComputeLineAmount(request.Quantity, request.Rate, request.DiscountPercent, request.VatPercent);
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(invoiceId, ct);
        return ToDto(await GetOwnedAsync(invoiceId, ct));
    }

    public async Task<PurchaseInvoiceDto> RemoveLineAsync(Guid invoiceId, Guid lineId, CancellationToken ct = default)
    {
        var invoice = await GetOwnedAsync(invoiceId, ct);
        EnsureDraft(invoice);

        var line = invoice.Lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new AppException("Invoice line not found.");
        _db.PurchaseInvoiceLines.Remove(line);
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(invoiceId, ct);
        return ToDto(await GetOwnedAsync(invoiceId, ct));
    }

    public async Task<PurchaseInvoiceDto> PostAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await GetOwnedAsync(invoiceId, ct);
        EnsureDraft(invoice);
        if (invoice.Lines.Count == 0)
            throw new AppException("Add at least one item before posting.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var line in invoice.Lines)
        {
            var warehouseId = line.Product.DefaultWarehouseId
                ?? throw new AppException($"'{line.Product.Name}' has no default warehouse — set one before posting this invoice.");
            _db.StockLedgerEntries.Add(new StockLedgerEntry
            {
                CompanyId = _currentUser.CompanyId,
                WarehouseId = warehouseId,
                ProductId = line.ProductId,
                MovementDate = today,
                QuantityIn = line.Quantity,
                ReferenceType = StockReferenceType.PurchaseInvoice,
                ReferenceId = invoice.Id,
                CreatedByUserId = _currentUser.UserId,
            });

            // Same reasoning as OpeningStockService: stock valuation prices
            // every unit at the product's own PurchasePrice, and this line's
            // Rate is often the first real cost ever entered for a product
            // that was created with none. Only fills in a zero/unset price —
            // never overwrites a price someone deliberately set, on this or
            // any later purchase.
            if (line.Rate > 0 && line.Product.PurchasePrice == 0)
                line.Product.PurchasePrice = line.Rate;
        }

        invoice.Status = DocumentStatus.Posted;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Posted", "Purchase.PurchaseInvoices", invoice.Id,
            $"posted invoice {invoice.InvoiceNumber} (Rs. {invoice.GrandTotalAmount:0.00})", ct);
        return ToDto(await GetOwnedAsync(invoiceId, ct));
    }

    public async Task<PurchaseInvoiceDto> CancelAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await GetOwnedAsync(invoiceId, ct);
        if (invoice.Status != DocumentStatus.Draft)
            throw new AppException(
                $"Invoice {invoice.InvoiceNumber} is {invoice.Status} — only a Draft invoice can be cancelled directly. " +
                "Use a Purchase Return to reverse a Posted one.");

        invoice.Status = DocumentStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Cancelled", "Purchase.PurchaseInvoices", invoice.Id, $"cancelled invoice {invoice.InvoiceNumber}", ct);
        return ToDto(invoice);
    }

    public async Task<PurchaseInvoiceDto> RecordPaymentAsync(Guid invoiceId, RecordPurchasePaymentRequest request, CancellationToken ct = default)
    {
        var invoice = await GetOwnedAsync(invoiceId, ct);
        if (invoice.Status != DocumentStatus.Posted)
            throw new AppException($"Invoice {invoice.InvoiceNumber} must be Posted before recording a payment against it.");
        if (request.Amount <= 0)
            throw new AppException("Payment amount must be greater than zero.");

        var remaining = invoice.GrandTotalAmount - invoice.AmountPaid;
        if (request.Amount > remaining)
            throw new AppException($"Payment of Rs. {request.Amount:0.00} exceeds the remaining balance of Rs. {remaining:0.00}.");

        invoice.AmountPaid += request.Amount;
        await _db.SaveChangesAsync(ct);
        return ToDto(invoice);
    }

    private async Task RecalculateTotalsAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await _db.PurchaseInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId, ct);

        var subTotal = invoice.Lines.Sum(l => l.Quantity * l.Rate);
        var discount = invoice.Lines.Sum(l => l.Quantity * l.Rate * l.DiscountPercent / 100m);
        var vat = invoice.Lines.Sum(l => (l.Quantity * l.Rate - l.Quantity * l.Rate * l.DiscountPercent / 100m) * l.VatPercent / 100m);
        var rawTotal = subTotal - discount + vat;
        var roundedTotal = Math.Round(rawTotal, 0, MidpointRounding.AwayFromZero);

        invoice.SubTotalAmount = Math.Round(subTotal, 2);
        invoice.DiscountAmount = Math.Round(discount, 2);
        invoice.VatAmount = Math.Round(vat, 2);
        invoice.RoundOffAmount = roundedTotal - rawTotal;
        invoice.GrandTotalAmount = roundedTotal;
        await _db.SaveChangesAsync(ct);
    }

    private static decimal ComputeLineAmount(decimal quantity, decimal rate, decimal discountPercent, decimal vatPercent)
    {
        var baseAmount = quantity * rate;
        var taxable = baseAmount - baseAmount * discountPercent / 100m;
        return Math.Round(taxable + taxable * vatPercent / 100m, 2);
    }

    private static void ValidateLineNumbers(decimal quantity, decimal rate, decimal discountPercent, decimal vatPercent)
    {
        if (quantity <= 0)
            throw new AppException("Quantity must be greater than zero.");
        if (rate < 0)
            throw new AppException("Rate can't be negative.");
        if (discountPercent is < 0 or > 100)
            throw new AppException("Discount % must be between 0 and 100.");
        if (vatPercent < 0)
            throw new AppException("Tax % can't be negative.");
    }

    private async Task<Product> ValidatePurchasableProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products.SingleOrDefaultAsync(
            p => p.Id == productId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct)
            ?? throw new AppException("The selected product does not exist.");

        // Only stocked types are purchasable from a supplier — a Recipe is built from its own
        // BOM ingredients (you buy those, not the finished dish), and a Service has no stock
        // to receive. See Product's class remarks for the full ProductType story.
        if (product.ProductType is ProductType.Recipe or ProductType.Service)
            throw new AppException($"'{product.Name}' is a {product.ProductType} product — it can't be purchased directly.");

        return product;
    }

    private async Task ValidateUnitAsync(Guid unitId, CancellationToken ct)
    {
        if (!await _db.Units.AnyAsync(u => u.Id == unitId && u.CompanyId == _currentUser.CompanyId && !u.IsDeleted, ct))
            throw new AppException("The selected unit does not exist.");
    }

    private async Task<string> GenerateNumberAsync(string prefix, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId;
        var numbers = await _db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId && i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync(ct);

        var next = numbers
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var num) ? num : 0)
            .DefaultIfEmpty(2000)
            .Max() + 1;

        return $"{prefix}{next}";
    }

    private static void EnsureDraft(PurchaseInvoice invoice)
    {
        if (invoice.Status != DocumentStatus.Draft)
            throw new AppException($"Invoice {invoice.InvoiceNumber} is {invoice.Status} and can no longer be changed.");
    }

    private async Task<PurchaseInvoice> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _db.PurchaseInvoices
            .Include(i => i.Supplier)
            .Include(i => i.Lines).ThenInclude(l => l.Product)
            .Include(i => i.Lines).ThenInclude(l => l.Unit)
            .SingleOrDefaultAsync(i => i.Id == id && i.CompanyId == _currentUser.CompanyId && !i.IsDeleted, ct);
        return invoice ?? throw new AppException("Purchase invoice not found.");
    }

    private static PurchaseInvoiceDto ToDto(PurchaseInvoice i) => new(
        i.Id, i.InvoiceNumber, i.SupplierId, i.Supplier.Name, i.SupplierReferenceNo,
        i.InvoiceDate, i.PaymentTerms, i.Status.ToString(),
        i.SubTotalAmount, i.DiscountAmount, i.VatAmount, i.RoundOffAmount, i.GrandTotalAmount,
        i.AmountPaid, i.GrandTotalAmount - i.AmountPaid, i.Narration,
        i.Lines.Select(l => new PurchaseInvoiceLineDto(
            l.Id, l.ProductId, l.Product.Name, l.UnitId, l.Unit.Name,
            l.Quantity, l.Rate, l.DiscountPercent, l.VatPercent, l.LineAmount)).ToList());
}
