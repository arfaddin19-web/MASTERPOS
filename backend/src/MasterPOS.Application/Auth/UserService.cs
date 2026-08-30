using MasterPOS.Application.Common;
using MasterPOS.Domain.Auth;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Auth;

/// <summary>
/// User accounts are never hard-deleted — too much of the schema references
/// Users.Id (CreatedByUserId, ApprovedByUserId, CashierUserId, ...) for that
/// to ever be safe. `PATCH .../active` (deactivate) is the only removal
/// path, same principle as Masters' transaction lock and Workforce's
/// Employee delete guard.
/// </summary>
public class UserService : IUserService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuditLogger _auditLogger;

    public UserService(MasterPosDbContext db, ICurrentUserContext currentUser, IPasswordHasher<User> passwordHasher, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new AppException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new AppException("Username is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new AppException("Password must be at least 6 characters.");
        await ValidateReferencesAsync(request.RoleId, request.DefaultBranchId, request.EmployeeId, ct);

        if (await _db.Users.AnyAsync(u => u.CompanyId == _currentUser.CompanyId && !u.IsDeleted && u.Username == request.Username, ct))
            throw new AppException($"Username '{request.Username}' is already taken.");

        var user = new User
        {
            CompanyId = _currentUser.CompanyId,
            RoleId = request.RoleId,
            DefaultBranchId = request.DefaultBranchId,
            EmployeeId = request.EmployeeId,
            FullName = request.FullName,
            Email = request.Email,
            Username = request.Username,
            IsActive = true,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Created", "Auth.Users", user.Id, $"created user '{user.Username}'", ct);
        return ToDto(await GetOwnedAsync(user.Id, ct));
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new AppException("Name is required.");
        await ValidateReferencesAsync(request.RoleId, request.DefaultBranchId, request.EmployeeId, ct);

        var user = await GetOwnedAsync(id, ct);
        user.FullName = request.FullName;
        user.Email = request.Email;
        user.RoleId = request.RoleId;
        user.DefaultBranchId = request.DefaultBranchId;
        user.EmployeeId = request.EmployeeId;
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task<UserDto> SetActiveAsync(Guid id, SetUserActiveRequest request, CancellationToken ct = default)
    {
        if (!request.IsActive && id == _currentUser.UserId)
            throw new AppException("You can't deactivate your own account.");

        var user = await GetOwnedAsync(id, ct);
        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync(
            request.IsActive ? "Updated" : "Deactivated", "Auth.Users", user.Id,
            $"{(request.IsActive ? "reactivated" : "deactivated")} user '{user.Username}'", ct);
        return ToDto(user);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            throw new AppException("Password must be at least 6 characters.");

        var user = await GetOwnedAsync(id, ct);
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Updated", "Auth.Users", user.Id, $"reset password for user '{user.Username}'", ct);
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<UserDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _db.Users
            .Include(u => u.Role)
            .Include(u => u.DefaultBranch)
            .Where(u => u.CompanyId == _currentUser.CompanyId && !u.IsDeleted);
        if (activeOnly) query = query.Where(u => u.IsActive);

        var users = await query.OrderBy(u => u.FullName).ToListAsync(ct);
        return users.Select(ToDto).ToList();
    }

    private async Task ValidateReferencesAsync(Guid roleId, Guid? defaultBranchId, Guid? employeeId, CancellationToken ct)
    {
        if (!await _db.Roles.AnyAsync(r => r.Id == roleId && r.CompanyId == _currentUser.CompanyId && !r.IsDeleted, ct))
            throw new AppException("The selected role does not exist.");
        if (defaultBranchId is { } branchId
            && !await _db.Branches.AnyAsync(b => b.Id == branchId && b.CompanyId == _currentUser.CompanyId && !b.IsDeleted, ct))
            throw new AppException("The selected branch does not exist.");
        if (employeeId is { } empId
            && !await _db.Employees.AnyAsync(e => e.Id == empId && e.CompanyId == _currentUser.CompanyId && !e.IsDeleted, ct))
            throw new AppException("The selected employee does not exist.");
    }

    private async Task<User> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.DefaultBranch)
            .SingleOrDefaultAsync(u => u.Id == id && u.CompanyId == _currentUser.CompanyId && !u.IsDeleted, ct);
        return user ?? throw new AppException("User not found.");
    }

    private static UserDto ToDto(User u) => new(
        u.Id, u.FullName, u.Email, u.Username, u.RoleId, u.Role.Name,
        u.DefaultBranchId, u.DefaultBranch?.Name, u.EmployeeId, u.IsActive, u.LastLoginAtUtc);
}
