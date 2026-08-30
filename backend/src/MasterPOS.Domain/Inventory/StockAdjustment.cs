using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Inventory;

/// <summary>Single-item corrections (breakage, count mismatch, expiry
/// write-off). Posting one writes a matching StockLedgerEntry.</summary>
public class StockAdjustment : CompanyOwnedEntity
{
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>Positive = found extra stock, negative = write-off.</summary>
    public decimal QuantityChange { get; set; }
    public string Reason { get; set; } = null!;
    public DateOnly AdjustmentDate { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
