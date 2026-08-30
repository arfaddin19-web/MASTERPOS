using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Inventory;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Inventory;

public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public StockAdjustmentService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentRequest request, CancellationToken ct = default)
    {
        if (request.QuantityChange == 0)
            throw new AppException("Quantity change can't be zero.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new AppException("A reason is required for a stock adjustment.");

        var product = await ValidateStockableProductAsync(request.ProductId, ct);
        await ValidateWarehouseAsync(request.WarehouseId, ct);

        var adjustment = new StockAdjustment
        {
            CompanyId = _currentUser.CompanyId,
            WarehouseId = request.WarehouseId,
            ProductId = product.Id,
            QuantityChange = request.QuantityChange,
            Reason = request.Reason,
            AdjustmentDate = request.AdjustmentDate,
        };
        _db.StockAdjustments.Add(adjustment);
        // Saved first so the DB-generated Id (NEWSEQUENTIALID()) is populated
        // back onto the tracked entity before the ledger entry references it.
        await _db.SaveChangesAsync(ct);

        // No Draft/Posted lifecycle here — creating an adjustment IS posting it.
        _db.StockLedgerEntries.Add(new StockLedgerEntry
        {
            CompanyId = _currentUser.CompanyId,
            WarehouseId = request.WarehouseId,
            ProductId = product.Id,
            MovementDate = request.AdjustmentDate,
            QuantityIn = request.QuantityChange > 0 ? request.QuantityChange : 0,
            QuantityOut = request.QuantityChange < 0 ? -request.QuantityChange : 0,
            ReferenceType = StockReferenceType.Adjustment,
            ReferenceId = adjustment.Id,
            CreatedByUserId = _currentUser.UserId,
        });
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(adjustment.Id, ct));
    }

    public async Task<StockAdjustmentDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<StockAdjustmentDto>> ListAsync(Guid? productId = null, Guid? warehouseId = null, CancellationToken ct = default)
    {
        var query = _db.StockAdjustments
            .Include(a => a.Product)
            .Include(a => a.Warehouse)
            .Where(a => a.CompanyId == _currentUser.CompanyId && !a.IsDeleted);

        if (productId is { } p) query = query.Where(a => a.ProductId == p);
        if (warehouseId is { } w) query = query.Where(a => a.WarehouseId == w);

        var adjustments = await query.OrderByDescending(a => a.AdjustmentDate).ToListAsync(ct);
        return adjustments.Select(ToDto).ToList();
    }

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

    private async Task<StockAdjustment> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var adjustment = await _db.StockAdjustments
            .Include(a => a.Product)
            .Include(a => a.Warehouse)
            .SingleOrDefaultAsync(a => a.Id == id && a.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct);
        return adjustment ?? throw new AppException("Stock adjustment not found.");
    }

    private static StockAdjustmentDto ToDto(StockAdjustment a) => new(
        a.Id, a.WarehouseId, a.Warehouse.Name, a.ProductId, a.Product.Name,
        a.QuantityChange, a.Reason, a.AdjustmentDate);
}
