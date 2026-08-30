using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Inventory;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Inventory;

public class OpeningStockService : IOpeningStockService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public OpeningStockService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OpeningStockDto> CreateAsync(CreateOpeningStockRequest request, CancellationToken ct = default)
    {
        if (request.Quantity < 0)
            throw new AppException("Quantity can't be negative.");
        if (request.UnitCost < 0)
            throw new AppException("Unit cost can't be negative.");

        var product = await ValidateStockableProductAsync(request.ProductId, ct);
        await ValidateWarehouseAsync(request.WarehouseId, ct);

        // The unique (WarehouseId, ProductId) index backs this up at the DB level too,
        // but a friendly 400 here beats a raw SQL constraint-violation error surfacing.
        var alreadySet = await _db.OpeningStocks.AnyAsync(
            o => o.WarehouseId == request.WarehouseId && o.ProductId == request.ProductId && !o.IsDeleted, ct);
        if (alreadySet)
            throw new AppException($"'{product.Name}' already has an opening stock set for this warehouse — use a Stock Adjustment to correct it.");

        var opening = new OpeningStock
        {
            CompanyId = _currentUser.CompanyId,
            WarehouseId = request.WarehouseId,
            ProductId = product.Id,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            AsOfDate = request.AsOfDate,
        };
        _db.OpeningStocks.Add(opening);
        await _db.SaveChangesAsync(ct);

        // No Draft/Posted lifecycle here either — creating the opening balance IS posting it.
        _db.StockLedgerEntries.Add(new StockLedgerEntry
        {
            CompanyId = _currentUser.CompanyId,
            WarehouseId = request.WarehouseId,
            ProductId = product.Id,
            MovementDate = request.AsOfDate,
            QuantityIn = request.Quantity,
            ReferenceType = StockReferenceType.OpeningStock,
            ReferenceId = opening.Id,
            CreatedByUserId = _currentUser.UserId,
        });
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(opening.Id, ct));
    }

    public async Task<OpeningStockDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<OpeningStockDto>> ListAsync(CancellationToken ct = default)
    {
        var openings = await _db.OpeningStocks
            .Include(o => o.Product)
            .Include(o => o.Warehouse)
            .Where(o => o.CompanyId == _currentUser.CompanyId && !o.IsDeleted)
            .OrderByDescending(o => o.AsOfDate)
            .ToListAsync(ct);
        return openings.Select(ToDto).ToList();
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

    private async Task<OpeningStock> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var opening = await _db.OpeningStocks
            .Include(o => o.Product)
            .Include(o => o.Warehouse)
            .SingleOrDefaultAsync(o => o.Id == id && o.CompanyId == _currentUser.CompanyId && !o.IsDeleted, ct);
        return opening ?? throw new AppException("Opening stock entry not found.");
    }

    private static OpeningStockDto ToDto(OpeningStock o) => new(
        o.Id, o.WarehouseId, o.Warehouse.Name, o.ProductId, o.Product.Name,
        o.Quantity, o.UnitCost, o.AsOfDate);
}
