using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Inventory;

/// <summary>
/// Append-only. Every stock-moving transaction (Purchase, PurchaseReturn,
/// an Order closing, Adjustment, Transfer, Opening Stock) writes exactly
/// one row here per product/warehouse affected — this is the single source
/// of truth for "closing stock", never a cached value on Product. Only
/// CreatedAtUtc/CreatedByUserId — no Modified*/IsDeleted, since a ledger
/// entry is never edited or soft-deleted, only ever appended to.
/// </summary>
public class StockLedgerEntry
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public DateOnly MovementDate { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public StockReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
