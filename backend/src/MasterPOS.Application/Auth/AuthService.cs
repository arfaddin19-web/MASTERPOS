using MasterPOS.Application.Common;
using MasterPOS.Domain.Auth;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Auth;

public class AuthService : IAuthService
{
    private readonly MasterPosDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(MasterPosDbContext db, IPasswordHasher<User> passwordHasher, ITokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // Single company per install (see Company's class remarks), so a
        // plain username lookup is enough — no CompanyId to disambiguate.
        var user = await _db.Users
            .Include(u => u.Role).ThenInclude(r => r.Permissions)
            .SingleOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted, ct);

        // Same rejection message whether the username doesn't exist, the
        // account is deactivated, or the password is wrong — never reveal
        // which one it was.
        if (user is null || !user.IsActive)
            throw new AppException("Invalid username or password.");

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
            throw new AppException("Invalid username or password.");

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var (token, expiresAtUtc) = _tokenService.CreateAccessToken(user, user.Role.Name);

        var permissions = user.Role.Permissions
            .Select(p => new PermissionDto(p.Module.ToString(), p.CanView, p.CanCreate, p.CanEdit, p.CanDelete, p.CanApprove))
            .ToList();

        return new LoginResponse(
            token,
            expiresAtUtc,
            user.Id,
            user.FullName,
            user.Username,
            user.CompanyId,
            user.DefaultBranchId,
            user.Role.Name,
            permissions);
    }
}
