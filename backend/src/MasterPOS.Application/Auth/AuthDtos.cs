namespace MasterPOS.Application.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string FullName,
    string Username,
    Guid CompanyId,
    Guid? DefaultBranchId,
    string RoleName,
    IReadOnlyList<PermissionDto> Permissions);

/// <summary>
/// One row of the permission matrix for the caller's role — mirrors the
/// Settings → Roles &amp; Permissions screen from the design phase, sent
/// back on login so the client can show/hide UI without a second call.
/// </summary>
public record PermissionDto(
    string Module,
    bool CanView,
    bool CanCreate,
    bool CanEdit,
    bool CanDelete,
    bool CanApprove);
