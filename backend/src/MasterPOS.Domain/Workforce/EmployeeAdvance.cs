using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Workforce;

public class EmployeeAdvance : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly AdvanceDate { get; set; }
    public string? Reason { get; set; }
    public decimal AmountRecovered { get; set; }
    public AdvanceStatus Status { get; set; } = AdvanceStatus.Open;

    public Employee Employee { get; set; } = null!;
}
