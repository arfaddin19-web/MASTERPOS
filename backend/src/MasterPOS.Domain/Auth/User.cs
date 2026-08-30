using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Workforce;

namespace MasterPOS.Domain.Auth;

public class User : CompanyOwnedEntity
{
    public Guid RoleId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public Guid? DefaultBranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }

    public Company Company { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public Employee? Employee { get; set; }
    public Branch? DefaultBranch { get; set; }
}
