using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Auth;

/// <summary>
/// One row per (Role, Module) — exactly the matrix shown in Settings →
/// Roles &amp; Permissions. No audit columns — this is a pure permission
/// matrix, not a business document (its own changes are what the Audit
/// Trail logs, not something it self-audits).
/// </summary>
public class RolePermission
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public PermissionModule Module { get; set; }
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }

    public Role Role { get; set; } = null!;
}
