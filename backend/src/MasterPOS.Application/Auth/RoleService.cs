using MasterPOS.Application.Common;
using MasterPOS.Domain.Auth;
using MasterPOS.Domain.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Auth;

public class RoleService : IRoleService
{
    private static readonly PermissionModule[] AllModules = Enum.GetValues<PermissionModule>();

    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public RoleService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<RoleDto> CreateAsync(UpsertRoleRequest request, CancellationToken ct = default)
    {
        var permissions = ValidatePermissions(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Role name is required.");
        if (await _db.Roles.AnyAsync(r => r.CompanyId == _currentUser.CompanyId && !r.IsDeleted && r.Name == request.Name, ct))
            throw new AppException($"A role named '{request.Name}' already exists.");

        var role = new Role { CompanyId = _currentUser.CompanyId, Name = request.Name, IsSystemRole = false };
        foreach (var (module, dto) in permissions)
            role.Permissions.Add(new RolePermission
            {
                Module = module, CanView = dto.CanView, CanCreate = dto.CanCreate,
                CanEdit = dto.CanEdit, CanDelete = dto.CanDelete, CanApprove = dto.CanApprove,
            });

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Created", "Auth.Roles", role.Id, $"created role '{role.Name}'", ct);
        return ToDto(await GetOwnedAsync(role.Id, ct));
    }

    public async Task<RoleDto> UpdateAsync(Guid id, UpsertRoleRequest request, CancellationToken ct = default)
    {
        var permissions = ValidatePermissions(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Role name is required.");

        var role = await GetOwnedAsync(id, ct);
        if (role.IsSystemRole)
            throw new AppException($"'{role.Name}' is a system role and can't be edited.");

        var duplicate = await _db.Roles.AnyAsync(
            r => r.Id != id && r.CompanyId == _currentUser.CompanyId && !r.IsDeleted && r.Name == request.Name, ct);
        if (duplicate)
            throw new AppException($"A role named '{request.Name}' already exists.");

        role.Name = request.Name;
        _db.RolePermissions.RemoveRange(role.Permissions);
        role.Permissions.Clear();
        foreach (var (module, dto) in permissions)
            role.Permissions.Add(new RolePermission
            {
                RoleId = role.Id, Module = module, CanView = dto.CanView, CanCreate = dto.CanCreate,
                CanEdit = dto.CanEdit, CanDelete = dto.CanDelete, CanApprove = dto.CanApprove,
            });
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await GetOwnedAsync(id, ct);
        if (role.IsSystemRole)
            throw new AppException($"'{role.Name}' is a system role and can't be deleted.");
        if (await _db.Users.AnyAsync(u => u.RoleId == id && !u.IsDeleted, ct))
            throw new AppException($"'{role.Name}' still has users assigned to it — reassign them first.");

        role.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Deleted", "Auth.Roles", role.Id, $"deleted role '{role.Name}'", ct);
    }

    public async Task<RoleDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles
            .Include(r => r.Permissions)
            .Where(r => r.CompanyId == _currentUser.CompanyId && !r.IsDeleted)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        return roles.Select(ToDto).ToList();
    }

    private static List<(PermissionModule Module, RolePermissionInput Dto)> ValidatePermissions(UpsertRoleRequest request)
    {
        var parsed = new List<(PermissionModule, RolePermissionInput)>();
        var seen = new HashSet<PermissionModule>();
        foreach (var p in request.Permissions)
        {
            if (!Enum.TryParse<PermissionModule>(p.Module, ignoreCase: true, out var module))
                throw new AppException($"Unknown module '{p.Module}'.");
            if (!seen.Add(module))
                throw new AppException($"Module '{module}' was specified more than once.");
            parsed.Add((module, p));
        }

        var missing = AllModules.Except(seen).ToList();
        if (missing.Count > 0)
            throw new AppException($"Missing permissions for: {string.Join(", ", missing)}.");

        return parsed;
    }

    private async Task<Role> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.Id == id && r.CompanyId == _currentUser.CompanyId && !r.IsDeleted, ct);
        return role ?? throw new AppException("Role not found.");
    }

    private static RoleDto ToDto(Role r) => new(
        r.Id, r.Name, r.IsSystemRole,
        r.Permissions.OrderBy(p => p.Module.ToString())
            .Select(p => new RolePermissionDto(p.Module.ToString(), p.CanView, p.CanCreate, p.CanEdit, p.CanDelete, p.CanApprove))
            .ToList());
}
