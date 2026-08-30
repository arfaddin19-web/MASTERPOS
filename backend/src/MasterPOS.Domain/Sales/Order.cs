using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Sales;

/// <summary>
/// One row per bill/table order — a Trading walk-in sale or a Cafe table
/// order alike. TableId/GuestCount are only populated for Cafe DineIn
/// orders.
/// </summary>
public class Order : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public OrderType OrderType { get; set; }
    public Guid? TableId { get; set; }
    public int? GuestCount { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid CashierUserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public decimal SubTotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal RoundOffAmount { get; set; }
    public decimal GrandTotalAmount { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    public Branch Branch { get; set; } = null!;
    public DiningTable? Table { get; set; }
    public Party? Customer { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
    public ICollection<OrderPayment> Payments { get; set; } = new List<OrderPayment>();

    /// <summary>Not mapped — SUM(Payments.Amount), the number the Split
    /// Payment screen calls "Paid".</summary>
    public decimal AmountPaid => Payments.Sum(p => p.Amount);

    /// <summary>Not mapped — the number the Split Payment screen calls
    /// "Remaining"; "Complete &amp; Close Bill" unlocks at exactly zero.</summary>
    public decimal AmountRemaining => GrandTotalAmount - AmountPaid;
}
