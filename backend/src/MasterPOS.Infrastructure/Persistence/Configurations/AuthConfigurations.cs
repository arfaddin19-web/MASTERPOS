using MasterPOS.Domain.Auth;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles", "Auth");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.IsSystemRole).HasDefaultValue(false);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CompanyId, e.Name }).IsUnique();
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions", "Auth", t => t.HasCheckConstraint(
            "CK_RolePermissions_Module",
            "[Module] IN (N'Billing', N'Masters', N'Inventory', N'Transactions', N'Reports', N'Workforce', N'Settings')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Module).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(e => e.CanView).HasDefaultValue(false);
        b.Property(e => e.CanCreate).HasDefaultValue(false);
        b.Property(e => e.CanEdit).HasDefaultValue(false);
        b.Property(e => e.CanDelete).HasDefaultValue(false);
        b.Property(e => e.CanApprove).HasDefaultValue(false);

        b.HasOne(e => e.Role).WithMany(r => r.Permissions)
            .HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.RoleId, e.Module }).IsUnique();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users", "Auth");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.FullName).HasMaxLength(150).IsRequired();
        b.Property(e => e.Email).HasMaxLength(200);
        b.Property(e => e.Username).HasMaxLength(100).IsRequired();
        b.Property(e => e.PasswordHash).HasMaxLength(300).IsRequired();
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.LastLoginAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Role).WithMany(r => r.Users).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.DefaultBranch).WithMany().HasForeignKey(e => e.DefaultBranchId).OnDelete(DeleteBehavior.Restrict);

        // Cross-module FK: EmployeeId -> Workforce.Employees. In the raw SQL
        // this is added by 07_Workforce_Payroll.sql (Employees doesn't exist
        // yet when 01_Core_Auth.sql runs); EF Core builds the whole model at
        // once so it's just configured here directly.
        b.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(e => new { e.CompanyId, e.Username }).IsUnique();
        b.HasIndex(e => e.CompanyId).HasFilter("[IsDeleted] = 0");
    }
}
