using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Inventory;

public class StockReportService : IStockReportService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public StockReportService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<StockLedgerEntryDto>> GetLedgerAsync(
        Guid? productId = null, Guid? warehouseId = null,
        DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default)
    {
        // The running balance is a per-product/warehouse concept, so it can only be computed
        // correctly against the product/warehouse's *entire* history — filtering by date range
        // happens after that running total is built, not before (a fromDate filter must not
        // reset the balance to zero at an arbitrary point).
        var query = _db.StockLedgerEntries
            .Include(e => e.Product)
            .Include(e => e.Warehouse)
            .Where(e => e.CompanyId == _currentUser.CompanyId);

        if (productId is { } p) query = query.Where(e => e.ProductId == p);
        if (warehouseId is { } w) query = query.Where(e => e.WarehouseId == w);

        var entries = await query
            .OrderBy(e => e.ProductId).ThenBy(e => e.WarehouseId)
            .ThenBy(e => e.MovementDate).ThenBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);

        var result = new List<StockLedgerEntryDto>();
        var running = new Dictionary<(Guid Product, Guid Warehouse), decimal>();
        foreach (var e in entries)
        {
            var key = (e.ProductId, e.WarehouseId);
            var balance = running.GetValueOrDefault(key) + e.QuantityIn - e.QuantityOut;
            running[key] = balance;

            if (fromDate is { } from && e.MovementDate < from) continue;
            if (toDate is { } to && e.MovementDate > to) continue;

            result.Add(new StockLedgerEntryDto(
                e.Id, e.MovementDate, e.ProductId, e.Product.Name, e.WarehouseId, e.Warehouse.Name,
                e.QuantityIn, e.QuantityOut, balance, e.ReferenceType.ToString(), e.ReferenceId));
        }

        return result.OrderByDescending(r => r.MovementDate).ThenBy(r => r.ProductName).ToList();
    }

    public async Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(Guid? warehouseId = null, CancellationToken ct = default)
    {
        var query = _db.StockLedgerEntries.Where(e => e.CompanyId == _currentUser.CompanyId);
        if (warehouseId is { } w) query = query.Where(e => e.WarehouseId == w);

        var grouped = await query
            .GroupBy(e => new { e.ProductId, e.WarehouseId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseId,
                Balance = g.Sum(e => e.QuantityIn) - g.Sum(e => e.QuantityOut),
            })
            .ToListAsync(ct);

        if (grouped.Count == 0) return Array.Empty<StockBalanceDto>();

        var productIds = grouped.Select(g => g.ProductId).Distinct().ToList();
        var warehouseIds = grouped.Select(g => g.WarehouseId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var warehouses = await _db.Warehouses.Where(w => warehouseIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);

        return grouped
            .Where(g => g.Balance != 0)
            .Select(g => new StockBalanceDto(
                g.ProductId, products[g.ProductId].Name,
                g.WarehouseId, warehouses[g.WarehouseId].Name, g.Balance))
            .OrderBy(b => b.ProductName).ToList();
    }

    public async Task<IReadOnlyList<ReorderSuggestionDto>> GetReorderSuggestionsAsync(CancellationToken ct = default)
    {
        // Reorder level is a company-wide setting on the product, so it's checked against
        // stock summed across all of the company's warehouses, not warehouse-by-warehouse.
        var balances = await _db.StockLedgerEntries
            .Where(e => e.CompanyId == _currentUser.CompanyId)
            .GroupBy(e => e.ProductId)
            .Select(g => new { ProductId = g.Key, Balance = g.Sum(e => e.QuantityIn) - g.Sum(e => e.QuantityOut) })
            .ToDictionaryAsync(g => g.ProductId, g => g.Balance, ct);

        var products = await _db.Products
            .Where(p => p.CompanyId == _currentUser.CompanyId && !p.IsDeleted && p.IsActive && p.ReorderLevel > 0)
            .ToListAsync(ct);

        return products
            .Select(p => new ReorderSuggestionDto(
                p.Id, p.Name, p.ReorderLevel, balances.GetValueOrDefault(p.Id), p.ReorderLevel - balances.GetValueOrDefault(p.Id)))
            .Where(s => s.CurrentBalance <= s.ReorderLevel)
            .OrderByDescending(s => s.ShortBy)
            .ToList();
    }
}
