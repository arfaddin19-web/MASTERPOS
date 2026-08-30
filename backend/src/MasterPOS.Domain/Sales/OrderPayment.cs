using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Sales;

/// <summary>
/// The Split Payment screen, one row per entry: any amount, any mode, an
/// optional label ("Guest 1"). Order.AmountPaid / Order.AmountRemaining
/// are just SUM(Amount) over these rows for one OrderId.
/// </summary>
public class OrderPayment : AuditableEntity
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public string? PaidByLabel { get; set; }
    public Guid CashierUserId { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
}
