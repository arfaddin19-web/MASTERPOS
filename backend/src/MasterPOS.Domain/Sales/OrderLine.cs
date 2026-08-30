using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Sales;

/// <summary>
/// Note: the per-item note field (filled in before "Save KOT"). KotStation
/// is snapshotted from the product at add-time so a later product-master
/// edit doesn't rewrite an already-punched order's history.
/// </summary>
public class OrderLine : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
    public KotStation? KotStation { get; set; }
    public KotLineStatus KotStatus { get; set; } = KotLineStatus.Pending;
    public decimal LineTotalAmount { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
