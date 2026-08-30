using MasterPOS.Domain.Core;
using MasterPOS.Domain.Auth;
using MasterPOS.Domain.Utility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class PrinterConfiguration : IEntityTypeConfiguration<Printer>
{
    public void Configure(EntityTypeBuilder<Printer> b)
    {
        b.ToTable("Printers", "Utility", t =>
        {
            t.HasCheckConstraint("CK_Printers_PrinterType", "[PrinterType] IN (N'Receipt', N'Kot')");
            t.HasCheckConstraint("CK_Printers_Station", "[Station] IS NULL OR [Station] IN (N'Kitchen', N'Bar')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.PrinterType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.Station).HasConversion<string?>().HasMaxLength(20);
        b.Property(e => e.ConnectionInfo).HasMaxLength(200);
        b.Property(e => e.IsEnabled).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentModeSettingConfiguration : IEntityTypeConfiguration<PaymentModeSetting>
{
    public void Configure(EntityTypeBuilder<PaymentModeSetting> b)
    {
        b.ToTable("PaymentModes", "Utility"); // C# name PaymentModeSetting, DB table PaymentModes — see class remarks
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Code).HasMaxLength(20).IsRequired();
        b.Property(e => e.IsEnabled).HasDefaultValue(true);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique();
    }
}

public class BackupLogEntryConfiguration : IEntityTypeConfiguration<BackupLogEntry>
{
    public void Configure(EntityTypeBuilder<BackupLogEntry> b)
    {
        b.ToTable("BackupLog", "Utility", t => t.HasCheckConstraint(
            "CK_BackupLog_Status", "[Status] IN (N'Success', N'Failed')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.BackupAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.FilePath).HasMaxLength(400).IsRequired();
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.BackupStatus.Success);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("AuditLog", "Utility");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Action).HasMaxLength(50).IsRequired();
        b.Property(e => e.EntityType).HasMaxLength(60).IsRequired();
        b.Property(e => e.Description).HasMaxLength(400).IsRequired();
        b.Property(e => e.OccurredAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.CompanyId, e.OccurredAtUtc }).IsDescending(false, true);
    }
}
