using MasterPOS.Domain.Core;
using MasterPOS.Domain.Auth;
using MasterPOS.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("Employees", "Workforce", t => t.HasCheckConstraint(
            "CK_Employees_MaritalStatus", "[MaritalStatus] IN (N'Single', N'Couple')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.FullName).HasMaxLength(150).IsRequired();
        b.Property(e => e.RoleTitle).HasMaxLength(100);
        b.Property(e => e.Phone).HasMaxLength(30);
        b.Property(e => e.BasicSalary).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.ShiftStart).HasColumnType("time(0)");
        b.Property(e => e.ShiftEnd).HasColumnType("time(0)");
        b.Property(e => e.MaritalStatus).HasConversion<string>().HasMaxLength(10).HasDefaultValue(Domain.Common.MaritalStatus.Single);
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> b)
    {
        b.ToTable("Attendance", "Workforce", t => t.HasCheckConstraint(
            "CK_Attendance_Status", "[Status] IN (N'Present', N'Late', N'Absent', N'OnLeave')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.CheckInAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.CheckOutAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.OvertimeHours).HasPrecision(5, 2).HasDefaultValue(0m);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.EmployeeId, e.AttendanceDate }).IsUnique();
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("LeaveRequests", "Workforce", t =>
        {
            t.HasCheckConstraint("CK_LeaveRequests_DateOrder", "[ToDate] >= [FromDate]");
            t.HasCheckConstraint("CK_LeaveRequests_Status", "[Status] IN (N'Pending', N'Approved', N'Rejected')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.LeaveType).HasMaxLength(30).IsRequired();
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.LeaveStatus.Pending);
        b.Property(e => e.Reason).HasMaxLength(300);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeeAdvanceConfiguration : IEntityTypeConfiguration<EmployeeAdvance>
{
    public void Configure(EntityTypeBuilder<EmployeeAdvance> b)
    {
        b.ToTable("EmployeeAdvances", "Workforce", t =>
        {
            t.HasCheckConstraint("CK_EmployeeAdvances_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_EmployeeAdvances_Status", "[Status] IN (N'Open', N'PartiallyRecovered', N'Recovered')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.Reason).HasMaxLength(300);
        b.Property(e => e.AmountRecovered).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.AdvanceStatus.Open);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> b)
    {
        b.ToTable("PayrollRuns", "Workforce", t =>
        {
            t.HasCheckConstraint("CK_PayrollRuns_PeriodMonth", "[PeriodMonth] BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_PayrollRuns_Status", "[Status] IN (N'Draft', N'Completed')");
            t.HasCheckConstraint("CK_PayrollRuns_RunType", "[RunType] IN (N'Monthly', N'FestivalBonus')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.PeriodMonth).HasColumnType("tinyint");
        b.Property(e => e.PeriodYear).HasColumnType("smallint");
        b.Property(e => e.RunType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.PayrollRunType.Monthly);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.PayrollRunStatus.Draft);
        b.Property(e => e.RunAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);

        // One run per Branch/Month/Year *per RunType* — a Monthly run and a
        // FestivalBonus run can coexist for the same period, since they're
        // different documents entirely, not two attempts at the same one.
        b.HasIndex(e => new { e.BranchId, e.PeriodYear, e.PeriodMonth, e.RunType }).IsUnique();
    }
}

public class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunLine> b)
    {
        b.ToTable("PayrollRunLines", "Workforce", t => t.HasCheckConstraint(
            "CK_PayrollRunLines_LineStatus", "[LineStatus] IN (N'Ready', N'LeaveDeduction', N'AttendancePending')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.BasicAmount).HasPrecision(18, 2);
        b.Property(e => e.AllowancesAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.OvertimeAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.DeductionsAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.AdvanceDeductionAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.PfEmployeeAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.PfEmployerAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.SsfEmployeeAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.SsfEmployerAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.TdsAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.NetPayAmount).HasPrecision(18, 2);
        b.Property(e => e.LineStatus).HasConversion<string>().HasMaxLength(30).HasDefaultValue(Domain.Common.PayrollLineStatus.Ready);

        b.HasOne(e => e.PayrollRun).WithMany(r => r.Lines).HasForeignKey(e => e.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.PayrollRunId, e.EmployeeId }).IsUnique();
    }
}

public class PayrollSettingsConfiguration : IEntityTypeConfiguration<PayrollSettings>
{
    public void Configure(EntityTypeBuilder<PayrollSettings> b)
    {
        b.ToTable("PayrollSettings", "Workforce", t =>
        {
            t.HasCheckConstraint("CK_PayrollSettings_OvertimeMultiplier", "[OvertimeMultiplier] >= 0");
            t.HasCheckConstraint("CK_PayrollSettings_PfPercents", "[PfEmployeePercent] >= 0 AND [PfEmployerPercent] >= 0");
            t.HasCheckConstraint("CK_PayrollSettings_SsfPercents", "[SsfEmployeePercent] >= 0 AND [SsfEmployerPercent] >= 0");
            t.HasCheckConstraint("CK_PayrollSettings_FestivalBonusPercent", "[FestivalBonusPercent] >= 0");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.OvertimeEnabled).HasDefaultValue(true);
        b.Property(e => e.OvertimeMultiplier).HasPrecision(5, 2).HasDefaultValue(1.5m);
        b.Property(e => e.PfEnabled).HasDefaultValue(false);
        b.Property(e => e.PfEmployeePercent).HasPrecision(5, 2).HasDefaultValue(10m);
        b.Property(e => e.PfEmployerPercent).HasPrecision(5, 2).HasDefaultValue(10m);
        b.Property(e => e.SsfEnabled).HasDefaultValue(false);
        b.Property(e => e.SsfEmployeePercent).HasPrecision(5, 2).HasDefaultValue(11m);
        b.Property(e => e.SsfEmployerPercent).HasPrecision(5, 2).HasDefaultValue(20m);
        b.Property(e => e.TdsEnabled).HasDefaultValue(false);
        b.Property(e => e.FestivalBonusEnabled).HasDefaultValue(false);
        b.Property(e => e.FestivalBonusPercent).HasPrecision(5, 2).HasDefaultValue(100m);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.CompanyId).IsUnique();
    }
}

public class TaxSlabConfiguration : IEntityTypeConfiguration<TaxSlab>
{
    public void Configure(EntityTypeBuilder<TaxSlab> b)
    {
        b.ToTable("TaxSlabs", "Workforce", t =>
        {
            t.HasCheckConstraint("CK_TaxSlabs_MaritalStatus", "[MaritalStatus] IN (N'Single', N'Couple')");
            t.HasCheckConstraint("CK_TaxSlabs_Bounds", "[UpperBound] IS NULL OR [UpperBound] > [LowerBound]");
            t.HasCheckConstraint("CK_TaxSlabs_RatePercent", "[RatePercent] BETWEEN 0 AND 100");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.MaritalStatus).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(e => e.LowerBound).HasPrecision(18, 2);
        b.Property(e => e.UpperBound).HasPrecision(18, 2);
        b.Property(e => e.RatePercent).HasPrecision(5, 2);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CompanyId, e.MaritalStatus, e.LowerBound });
    }
}
