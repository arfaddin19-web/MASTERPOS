using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Workforce;

public class LeaveRequest : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public string LeaveType { get; set; } = null!;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public Guid? ApprovedByUserId { get; set; }
    public string? Reason { get; set; }

    public Employee Employee { get; set; } = null!;
}
