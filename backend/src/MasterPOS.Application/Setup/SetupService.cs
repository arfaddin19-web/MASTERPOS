using MasterPOS.Application.Common;
using MasterPOS.Domain.Auth;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Setup;

/// <summary>
/// Drives the First-Time Setup wizard. One local install = exactly one
/// Company (see Company's class remarks), so this can run exactly once —
/// a second call is rejected outright rather than creating a duplicate.
/// </summary>
public class SetupService : ISetupService
{
    // Every module gets full rights on the Admin role this creates — the
    // values match CK_RolePermissions_Module's list exactly.
    private static readonly PermissionModule[] AllModules = Enum.GetValues<PermissionModule>();

    private readonly MasterPosDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public SetupService(MasterPosDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<SetupStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var exists = await _db.Companies.AnyAsync(ct);
        return new SetupStatusResponse(exists);
    }

    public async Task<SetupCompanyResponse> CompleteSetupAsync(SetupCompanyRequest request, CancellationToken ct = default)
    {
        if (await _db.Companies.AnyAsync(ct))
            throw new AppException("Setup has already been completed on this install.");

        if (!Enum.TryParse<BusinessType>(request.BusinessType, ignoreCase: true, out var businessType))
            throw new AppException($"Unknown business type '{request.BusinessType}'.");

        if (!Enum.TryParse<TaxRegistrationType>(request.TaxRegistrationType, ignoreCase: true, out var taxRegType))
            throw new AppException($"Unknown tax registration type '{request.TaxRegistrationType}'.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var company = new Company
        {
            Name = request.CompanyName,
            BusinessType = businessType,
            TaxRegistrationType = taxRegType,
            VatRegistrationNumber = request.VatRegistrationNumber,
            VatRatePercent = request.VatRatePercent,
            PayrollEnabled = request.PayrollEnabled,
        };
        _db.Companies.Add(company);

        var branch = new Branch
        {
            Company = company,
            Name = request.BranchName,
            City = request.City,
            Address = request.Address,
            Phone = request.Phone,
            IsPrimary = true,
        };
        _db.Branches.Add(branch);

        var adminRole = new Role
        {
            Company = company,
            Name = "Admin",
            IsSystemRole = true,
        };
        foreach (var module in AllModules)
        {
            adminRole.Permissions.Add(new RolePermission
            {
                Role = adminRole,
                Module = module,
                CanView = true,
                CanCreate = true,
                CanEdit = true,
                CanDelete = true,
                CanApprove = true,
            });
        }
        _db.Roles.Add(adminRole);

        var adminUser = new User
        {
            Company = company,
            Role = adminRole,
            DefaultBranch = branch,
            FullName = request.AdminFullName,
            Username = request.AdminUsername,
            Email = request.AdminEmail,
            IsActive = true,
        };
        // PasswordHasher only reads the entity for its salt/algorithm
        // context — it never touches PasswordHash itself, so hashing
        // before the field is set is safe.
        adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, request.AdminPassword);
        _db.Users.Add(adminUser);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SetupCompanyResponse(company.Id, branch.Id, adminUser.Id, adminRole.Id);
    }
}
