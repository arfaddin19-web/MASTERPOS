using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Workforce;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Workforce;

/// <summary>
/// Drives the Payroll tab's "Run Payroll" button (Monthly) and the separate
/// once-a-year Festival Bonus batch (FestivalBonus) — same PayrollRun/
/// PayrollRunLine shape, different calculations. A Monthly run computes one
/// line per active employee: Basic pro-rated for unpaid absence, Overtime
/// from Attendance.OvertimeHours (only when PayrollSettings.OvertimeEnabled),
/// PF/SSF employee+employer contributions (only when enabled), TDS via the
/// company's own TaxSlabs table (only when enabled), and an Advance
/// deduction capped at that line's own gross pay. Every rate/toggle comes
/// from PayrollSettings, read live at compute time — nothing here is a
/// hardcoded policy. Stays Draft (fully re-computable) until Complete locks
/// it and actually recovers the advances.
/// </summary>
public class PayrollRunService : IPayrollRunService
{
    // Standard-day assumption for turning a daily rate into an hourly OT
    // rate. No shift-hours-per-day policy exists elsewhere in the schema,
    // so it's a documented default, not a guess hidden in the math.
    private const decimal StandardHoursPerDay = 8m;

    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public PayrollRunService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<PayrollRunDto> CreateAsync(CreatePayrollRunRequest request, CancellationToken ct = default)
    {
        if (request.PeriodMonth is < 1 or > 12)
            throw new AppException("Period month must be between 1 and 12.");
        var runType = ParseRunType(request.RunType);

        if (!await _db.Branches.AnyAsync(b => b.Id == request.BranchId && b.CompanyId == _currentUser.CompanyId && !b.IsDeleted, ct))
            throw new AppException("The selected branch does not exist.");

        var settings = await GetOrCreateSettingsAsync(ct);
        if (runType == PayrollRunType.FestivalBonus && !settings.FestivalBonusEnabled)
            throw new AppException("Festival Bonus is turned off in Payroll Settings — enable it first.");

        var exists = await _db.PayrollRuns.AnyAsync(r =>
            r.BranchId == request.BranchId && r.PeriodYear == request.PeriodYear &&
            r.PeriodMonth == request.PeriodMonth && r.RunType == runType && !r.IsDeleted, ct);
        if (exists)
            throw new AppException($"A {runType} run for {request.PeriodMonth:00}/{request.PeriodYear} already exists for this branch.");

        var run = new PayrollRun
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = request.BranchId,
            PeriodMonth = request.PeriodMonth,
            PeriodYear = request.PeriodYear,
            RunType = runType,
            Status = PayrollRunStatus.Draft,
        };
        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        await GenerateLinesAsync(run, settings, ct);
        return ToDto(await GetOwnedAsync(run.Id, ct));
    }

    public async Task<PayrollRunDto> RecomputeAsync(Guid id, CancellationToken ct = default)
    {
        var run = await GetOwnedAsync(id, ct);
        EnsureDraft(run);
        var settings = await GetOrCreateSettingsAsync(ct);

        _db.PayrollRunLines.RemoveRange(run.Lines);
        await _db.SaveChangesAsync(ct);

        await GenerateLinesAsync(run, settings, ct);
        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task<PayrollRunDto> CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var run = await GetOwnedAsync(id, ct);
        EnsureDraft(run);
        if (run.Lines.Count == 0)
            throw new AppException("This run has no lines to complete — recompute it first.");

        foreach (var line in run.Lines.Where(l => l.AdvanceDeductionAmount > 0))
            await RecoverAdvancesAsync(line.EmployeeId, line.AdvanceDeductionAmount, ct);

        run.Status = PayrollRunStatus.Completed;
        run.RunAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Completed", "Workforce.PayrollRuns", run.Id,
            $"completed {run.RunType} payroll for {run.PeriodMonth:00}/{run.PeriodYear}", ct);

        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task<PayrollRunDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<PayrollRunDto>> ListAsync(Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _db.PayrollRuns
            .Include(r => r.Branch)
            .Include(r => r.Lines).ThenInclude(l => l.Employee)
            .Where(r => r.CompanyId == _currentUser.CompanyId && !r.IsDeleted);
        if (branchId is { } b) query = query.Where(r => r.BranchId == b);

        var runs = await query.OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth).ToListAsync(ct);
        return runs.Select(ToDto).ToList();
    }

    /// <summary>Recovers `amount` from the employee's oldest outstanding advances
    /// first (FIFO), spreading it across as many as needed.</summary>
    private async Task RecoverAdvancesAsync(Guid employeeId, decimal amount, CancellationToken ct)
    {
        var openAdvances = await _db.EmployeeAdvances
            .Where(a => a.EmployeeId == employeeId && a.Status != AdvanceStatus.Recovered && !a.IsDeleted)
            .OrderBy(a => a.AdvanceDate)
            .ToListAsync(ct);

        var remaining = amount;
        foreach (var advance in openAdvances)
        {
            if (remaining <= 0) break;
            var balance = advance.Amount - advance.AmountRecovered;
            var recover = Math.Min(balance, remaining);
            advance.AmountRecovered += recover;
            advance.Status = advance.AmountRecovered >= advance.Amount ? AdvanceStatus.Recovered : AdvanceStatus.PartiallyRecovered;
            remaining -= recover;
        }
    }

    private async Task GenerateLinesAsync(PayrollRun run, PayrollSettings settings, CancellationToken ct)
    {
        var employees = await _db.Employees
            .Where(e => e.CompanyId == _currentUser.CompanyId && e.BranchId == run.BranchId && e.IsActive && !e.IsDeleted)
            .ToListAsync(ct);
        if (employees.Count == 0) return;

        if (run.RunType == PayrollRunType.FestivalBonus)
        {
            await GenerateFestivalBonusLinesAsync(run, employees, settings, ct);
            return;
        }

        var daysInMonth = DateTime.DaysInMonth(run.PeriodYear, run.PeriodMonth);
        var periodStart = new DateOnly(run.PeriodYear, run.PeriodMonth, 1);
        var periodEnd = new DateOnly(run.PeriodYear, run.PeriodMonth, daysInMonth);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // How many days of this period should already have an attendance mark —
        // the full month once it's over, day-of-month while it's current, zero
        // for a period that hasn't started. Drives the AttendancePending flag.
        var expectedMarkedDays = today >= periodEnd ? daysInMonth : today < periodStart ? 0 : today.Day;

        var employeeIds = employees.Select(e => e.Id).ToList();
        var attendanceByEmployee = (await _db.Attendances
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.AttendanceDate >= periodStart && a.AttendanceDate <= periodEnd && !a.IsDeleted)
            .ToListAsync(ct))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var outstandingAdvanceByEmployee = (await _db.EmployeeAdvances
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.Status != AdvanceStatus.Recovered && !a.IsDeleted)
            .ToListAsync(ct))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount - a.AmountRecovered));

        var taxSlabs = settings.TdsEnabled
            ? await _db.TaxSlabs.Where(s => s.CompanyId == _currentUser.CompanyId && !s.IsDeleted).OrderBy(s => s.LowerBound).ToListAsync(ct)
            : new List<TaxSlab>();

        foreach (var employee in employees)
        {
            var records = attendanceByEmployee.GetValueOrDefault(employee.Id, new List<Attendance>());
            var absentCount = records.Count(a => a.Status == AttendanceStatus.Absent);
            var markedCount = records.Count;
            var overtimeHours = records.Sum(a => a.OvertimeHours);

            var dailyRate = daysInMonth > 0 ? employee.BasicSalary / daysInMonth : 0m;
            var basicAmount = Math.Max(0m, Math.Round(employee.BasicSalary - dailyRate * absentCount, 2));

            var overtimeAmount = 0m;
            if (settings.OvertimeEnabled)
            {
                var otHourRate = Math.Round(dailyRate / StandardHoursPerDay * settings.OvertimeMultiplier, 2);
                overtimeAmount = Math.Round(overtimeHours * otHourRate, 2);
            }

            decimal pfEmployeeAmount = 0m, pfEmployerAmount = 0m;
            if (settings.PfEnabled)
            {
                pfEmployeeAmount = Math.Round(basicAmount * settings.PfEmployeePercent / 100m, 2);
                pfEmployerAmount = Math.Round(basicAmount * settings.PfEmployerPercent / 100m, 2);
            }

            decimal ssfEmployeeAmount = 0m, ssfEmployerAmount = 0m;
            if (settings.SsfEnabled)
            {
                ssfEmployeeAmount = Math.Round(basicAmount * settings.SsfEmployeePercent / 100m, 2);
                ssfEmployerAmount = Math.Round(basicAmount * settings.SsfEmployerPercent / 100m, 2);
            }

            var tdsAmount = 0m;
            if (settings.TdsEnabled)
            {
                // PF/SSF employee contributions are pre-tax deductible — a
                // standard, defensible simplification, not a guess: both
                // schemes exist specifically as tax-advantaged retirement
                // savings under Nepali law.
                var monthlyTaxable = Math.Max(0m, basicAmount + overtimeAmount - pfEmployeeAmount - ssfEmployeeAmount);
                tdsAmount = ComputeMonthlyTds(monthlyTaxable, employee.MaritalStatus, taxSlabs);
            }

            var grossBeforeAdvance = basicAmount + overtimeAmount;
            var netBeforeAdvance = Math.Max(0m,
                grossBeforeAdvance - pfEmployeeAmount - ssfEmployeeAmount - tdsAmount);
            var outstandingAdvance = outstandingAdvanceByEmployee.GetValueOrDefault(employee.Id, 0m);
            var advanceDeduction = Math.Min(outstandingAdvance, netBeforeAdvance);

            var lineStatus = markedCount < expectedMarkedDays
                ? PayrollLineStatus.AttendancePending
                : absentCount > 0
                    ? PayrollLineStatus.LeaveDeduction
                    : PayrollLineStatus.Ready;

            _db.PayrollRunLines.Add(new PayrollRunLine
            {
                PayrollRunId = run.Id,
                EmployeeId = employee.Id,
                BasicAmount = basicAmount,
                OvertimeAmount = overtimeAmount,
                PfEmployeeAmount = pfEmployeeAmount,
                PfEmployerAmount = pfEmployerAmount,
                SsfEmployeeAmount = ssfEmployeeAmount,
                SsfEmployerAmount = ssfEmployerAmount,
                TdsAmount = tdsAmount,
                AdvanceDeductionAmount = advanceDeduction,
                NetPayAmount = netBeforeAdvance - advanceDeduction,
                LineStatus = lineStatus,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Annualizes this month's taxable pay (×12), walks the
    /// progressive slab table for the employee's marital status, then
    /// divides the resulting annual tax back down to a monthly figure. This
    /// is the standard small-business simplification — it assumes the
    /// month's pay repeats all year — not a cumulative year-to-date
    /// withholding calculation; a bonus month will over-withhold slightly
    /// and true up at year end, same as most simple payroll tools.</summary>
    private static decimal ComputeMonthlyTds(decimal monthlyTaxable, MaritalStatus status, List<TaxSlab> allSlabs)
    {
        var slabs = allSlabs.Where(s => s.MaritalStatus == status).OrderBy(s => s.LowerBound).ToList();
        if (slabs.Count == 0) return 0m;

        var annualTaxable = monthlyTaxable * 12m;
        var annualTax = 0m;
        foreach (var slab in slabs)
        {
            if (annualTaxable <= slab.LowerBound) break;
            var bandTop = slab.UpperBound is { } upper ? Math.Min(upper, annualTaxable) : annualTaxable;
            var bandAmount = bandTop - slab.LowerBound;
            if (bandAmount <= 0) continue;
            annualTax += bandAmount * slab.RatePercent / 100m;
        }

        return Math.Round(annualTax / 12m, 2);
    }

    /// <summary>The once-a-year Festival Bonus batch — no attendance/OT/PF/
    /// SSF/TDS logic at all, just Basic Salary × FestivalBonusPercent per
    /// active employee, carried in AllowancesAmount since a bonus is exactly
    /// that: an allowance, not wages.</summary>
    private Task GenerateFestivalBonusLinesAsync(PayrollRun run, List<Employee> employees, PayrollSettings settings, CancellationToken ct)
    {
        foreach (var employee in employees)
        {
            var bonus = Math.Round(employee.BasicSalary * settings.FestivalBonusPercent / 100m, 2);
            _db.PayrollRunLines.Add(new PayrollRunLine
            {
                PayrollRunId = run.Id,
                EmployeeId = employee.Id,
                BasicAmount = 0m,
                AllowancesAmount = bonus,
                NetPayAmount = bonus,
                LineStatus = PayrollLineStatus.Ready,
            });
        }
        return _db.SaveChangesAsync(ct);
    }

    private async Task<PayrollSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.PayrollSettings.SingleOrDefaultAsync(
            s => s.CompanyId == _currentUser.CompanyId && !s.IsDeleted, ct);
        if (settings is not null) return settings;

        settings = new PayrollSettings { CompanyId = _currentUser.CompanyId };
        _db.PayrollSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private static PayrollRunType ParseRunType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return PayrollRunType.Monthly;
        if (!Enum.TryParse<PayrollRunType>(value, ignoreCase: true, out var parsed))
            throw new AppException($"Unknown run type '{value}'.");
        return parsed;
    }

    private static void EnsureDraft(PayrollRun run)
    {
        if (run.Status != PayrollRunStatus.Draft)
            throw new AppException($"This payroll run is {run.Status} and can no longer be changed.");
    }

    private async Task<PayrollRun> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var run = await _db.PayrollRuns
            .Include(r => r.Branch)
            .Include(r => r.Lines).ThenInclude(l => l.Employee)
            .SingleOrDefaultAsync(r => r.Id == id && r.CompanyId == _currentUser.CompanyId && !r.IsDeleted, ct);
        return run ?? throw new AppException("Payroll run not found.");
    }

    private static PayrollRunDto ToDto(PayrollRun r)
    {
        var lines = r.Lines
            .OrderBy(l => l.Employee.FullName)
            .Select(l => new PayrollRunLineDto(
                l.Id, l.EmployeeId, l.Employee.FullName, l.Employee.RoleTitle,
                l.BasicAmount, l.AllowancesAmount, l.OvertimeAmount, l.DeductionsAmount,
                l.PfEmployeeAmount, l.PfEmployerAmount, l.SsfEmployeeAmount, l.SsfEmployerAmount,
                l.TdsAmount, l.AdvanceDeductionAmount, l.NetPayAmount, l.LineStatus.ToString()))
            .ToList();

        var gross = r.Lines.Sum(l => l.BasicAmount + l.AllowancesAmount + l.OvertimeAmount);
        var net = r.Lines.Sum(l => l.NetPayAmount);

        return new PayrollRunDto(r.Id, r.BranchId, r.Branch.Name, r.PeriodMonth, r.PeriodYear, r.RunType.ToString(),
            r.Status.ToString(), r.RunAtUtc, gross, net, lines);
    }
}
