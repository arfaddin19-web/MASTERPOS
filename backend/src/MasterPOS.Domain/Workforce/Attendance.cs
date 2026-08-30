using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Workforce;

public class Attendance : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public DateTime? CheckInAtUtc { get; set; }
    public DateTime? CheckOutAtUtc { get; set; }
    public AttendanceStatus Status { get; set; }
    public decimal OvertimeHours { get; set; }

    public Employee Employee { get; set; } = null!;
}
