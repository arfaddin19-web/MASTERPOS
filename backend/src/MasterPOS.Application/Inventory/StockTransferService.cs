using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Inventory;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Inventory;

public class StockTransferService : IStockTransferService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public StockTransferService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<StockTransferDto> CreateAsync(CreateStockTransferRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
            throw new AppException("Quantity must be greater than zero.");
        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new AppException("Source and destination warehouse must be different.");

        var product = await ValidateStockableProductAsync(request.ProductId, ct);
        await ValidateWarehouseAsync(request.FromWarehouseId, ct);
        await ValidateWarehouseAsync(request.ToWarehouseId, ct);

        var transfer = new StockTransfer
        {
            CompanyId = _currentUser.CompanyId,
            ProductId = product.Id,
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            Quantity = request.Quantity,
            TransferDate = request.TransferDate,
            // Overriding the entity's own Completed default: this module always
            // creates Pending and moves stock only on the explicit Post below —
            // matching the Draft→Posted discipline every other document here uses.
            Status = StockTransferStatus.Pending,
        };
        _db.StockTransfers.Add(transfer);
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(transfer.Id, ct));
    }

    public async Task<StockTransferDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<StockTransferDto>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _db.StockTransfers
            .Include(t => t.Product)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Where(t => t.CompanyId == _currentUser.CompanyId && !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StockTransferStatus>(status, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown status '{status}'.");
            query = query.Where(t => t.Status == parsed);
        }

        var transfers = await query.OrderByDescending(t => t.TransferDate).ToListAsync(ct);
        return transfers.Select(ToDto).ToList();
    }

    public async Task<StockTransferDto> PostAsync(Guid id, CancellationToken ct = default)
    {
        var transfer = await GetOwnedAsync(id, ct);
        EnsurePending(transfer);

        var available = await GetBalanceAsync(transfer.ProductId, transfer.FromWarehouseId, ct);
        if (transfer.Quantity > available)
            throw new AppException(
                $"'{transfer.Product.Name}' only has {available:0.###} available at " +
                $"'{transfer.FromWarehouse.Name}' — can't transfer {transfer.Quantity:0.###}.");

        _db.StockLedgerEntries.Add(new StockLedgerEntry
        {
            CompanyId = _currentUser.CompanyId,
            WarehouseId = transfer.FromWarehouseId,
            ProductId = transfer.ProductId,
            MovementDate = transfer.TransferDate,
            QuantityOut = transfer.Quantity,
            ReferenceType = StockReferenceType.TransferOut,
            ReferenceId = transfer.Id,
            CreatedByUserId = _currentUser.UserId,
        });
        _db.StockLedgerEntries.Add(new StockLedgerEntry
        {
            CompanyId = _currentUser.CompanyId,
            WarehouseId = transfer.ToWarehouseId,
            ProductId = transfer.ProductId,
            MovementDate = transfer.TransferDate,
            QuantityIn = transfer.Quantity,
            ReferenceType = StockReferenceType.TransferIn,
            ReferenceId = transfer.Id,
            CreatedByUserId = _currentUser.UserId,
        });

        transfer.Status = StockTransferStatus.Completed;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Posted", "Inventory.StockTransfers", transfer.Id,
            $"transferred {transfer.Quantity:0.###} of '{transfer.Product.Name}' from '{transfer.FromWarehouse.Name}' to '{transfer.ToWarehouse.Name}'", ct);
        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task<StockTransferDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var transfer = await GetOwnedAsync(id, ct);
        EnsurePending(transfer);

        transfer.Status = StockTransferStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return ToDto(transfer);
    }

    private async Task<decimal> GetBalanceAsync(Guid productId, Guid warehouseId, CancellationToken ct)
        => await _db.StockLedgerEntries
            .Where(e => e.ProductId == productId && e.WarehouseId == warehouseId)
            .SumAsync(e => (decimal?)(e.QuantityIn - e.QuantityOut), ct) ?? 0m;

    private async Task<Product> ValidateStockableProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products.SingleOrDefaultAsync(
            p => p.Id == productId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct)
            ?? throw new AppException("The selected product does not exist.");
        if (product.ProductType is ProductType.Recipe or ProductType.Service)
            throw new AppException($"'{product.Name}' is a {product.ProductType} product — it doesn't hold its own stock.");
        return product;
    }

    private async Task ValidateWarehouseAsync(Guid warehouseId, CancellationToken ct)
    {
        if (!await _db.Warehouses.AnyAsync(w => w.Id == warehouseId && w.CompanyId == _currentUser.CompanyId && !w.IsDeleted, ct))
            throw new AppException("The selected warehouse does not exist.");
    }

    private static void EnsurePending(StockTransfer transfer)
    {
        if (transfer.Status != StockTransferStatus.Pending)
            throw new AppException($"This transfer is {transfer.Status} and can no longer be changed.");
    }

    private async Task<StockTransfer> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var transfer = await _db.StockTransfers
            .Include(t => t.Product)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .SingleOrDefaultAsync(t => t.Id == id && t.CompanyId == _currentUser.CompanyId && !t.IsDeleted, ct);
        return transfer ?? throw new AppException("Stock transfer not found.");
    }

    private static StockTransferDto ToDto(StockTransfer t) => new(
        t.Id, t.ProductId, t.Product.Name,
        t.FromWarehouseId, t.FromWarehouse.Name,
        t.ToWarehouseId, t.ToWarehouse.Name,
        t.Quantity, t.TransferDate, t.Status.ToString());
}
