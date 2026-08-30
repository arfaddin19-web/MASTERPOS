using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;

namespace MasterPOS.Domain.Workforce;

public class Employee : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string FullName { get; set; } = null!;
    public string? RoleTitle { get; set; }
    public string? Phone { get; set; }
    public DateOnly JoinDate { get; set; }
    public decimal BasicSalary { get; set; }
    public TimeOnly? ShiftStart { get; set; }
    public TimeOnly? ShiftEnd { get; set; }
    /// <summary>Which Nepal income-tax slab table a TDS calculation reads
    /// for this employee — Single and Couple have different band widths.</summary>
    public MaritalStatus MaritalStatus { get; set; } = MaritalStatus.Single;
    public bool IsActive { get; set; } = true;

    public Branch Branch { get; set; } = null!;
}
