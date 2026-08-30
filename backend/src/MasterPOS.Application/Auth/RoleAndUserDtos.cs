namespace MasterPOS.Application.Auth;

// ---- Roles ----

public record RolePermissionDto(string Module, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, bool CanApprove);

public record RolePermissionInput(string Module, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, bool CanApprove);

public record RoleDto(Guid Id, string Name, bool IsSystemRole, IReadOnlyList<RolePermissionDto> Permissions);

/// <summary>Permissions must cover every PermissionModule exactly once —
/// same list CK_RolePermissions_Module enforces — so the matrix this
/// produces can never have a silently-missing module.</summary>
public record UpsertRoleRequest(string Name, IReadOnlyList<RolePermissionInput> Permissions);

// ---- Users ----

public record UserDto(
    Guid Id, string FullName, string? Email, string Username,
    Guid RoleId, string RoleName, Guid? DefaultBranchId, string? DefaultBranchName,
    Guid? EmployeeId, bool IsActive, DateTime? LastLoginAtUtc);

public record CreateUserRequest(
    string FullName, string? Email, string Username, string Password,
    Guid RoleId, Guid? DefaultBranchId, Guid? EmployeeId);

public record UpdateUserRequest(
    string FullName, string? Email, Guid RoleId, Guid? DefaultBranchId, Guid? EmployeeId);

public record SetUserActiveRequest(bool IsActive);

public record ResetPasswordRequest(string NewPassword);
