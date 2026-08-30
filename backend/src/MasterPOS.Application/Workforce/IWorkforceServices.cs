namespace MasterPOS.Application.Workforce;

public interface IEmployeeService
{
    Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default);
    Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct = default);
    Task<EmployeeDto> SetActiveAsync(Guid id, SetEmployeeActiveRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default);
}

public interface IAttendanceService
{
    Task<AttendanceDto> CheckInAsync(CheckInRequest request, CancellationToken ct = default);
    Task<AttendanceDto> CheckOutAsync(Guid id, CancellationToken ct = default);
    /// <summary>Manual back-office mark/correction. Upserts — a second call for
    /// the same employee + date updates the existing row instead of failing on
    /// the (EmployeeId, AttendanceDate) unique index.</summary>
    Task<AttendanceDto> MarkAsync(MarkAttendanceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceDto>> ListAsync(
        Guid? employeeId = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);
    /// <summary>The "Today's Attendance Snapshot" card — every active employee
    /// in the caller's branch, whether or not they've been marked yet today.</summary>
    Task<IReadOnlyList<TodayAttendanceRowDto>> GetTodaySnapshotAsync(CancellationToken ct = default);
}

public interface ILeaveRequestService
{
    Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestRequest request, CancellationToken ct = default);
    Task<LeaveRequestDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<LeaveRequestDto> RejectAsync(Guid id, CancellationToken ct = default);
    Task<LeaveRequestDto> CancelAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LeaveRequestDto>> ListAsync(Guid? employeeId = null, string? status = null, CancellationToken ct = default);
}

public interface IEmployeeAdvanceService
{
    Task<EmployeeAdvanceDto> CreateAsync(CreateEmployeeAdvanceRequest request, CancellationToken ct = default);
    Task<EmployeeAdvanceDto> RecordRecoveryAsync(Guid id, RecordAdvanceRecoveryRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeAdvanceDto>> ListAsync(Guid? employeeId = null, string? status = null, CancellationToken ct = default);
}

public interface IPayrollSettingsService
{
    /// <summary>Auto-creates a default (everything off except Overtime) row
    /// on first access — there's no explicit "initialize settings" step,
    /// same as how a fresh install works before anyone visits the screen.</summary>
    Task<PayrollSettingsDto> GetAsync(CancellationToken ct = default);
    Task<PayrollSettingsDto> UpdateAsync(UpdatePayrollSettingsRequest request, CancellationToken ct = default);
}

public interface ITaxSlabService
{
    Task<IReadOnlyList<TaxSlabDto>> ListAsync(CancellationToken ct = default);
    Task<TaxSlabDto> CreateAsync(UpsertTaxSlabRequest request, CancellationToken ct = default);
    Task<TaxSlabDto> UpdateAsync(Guid id, UpsertTaxSlabRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Seeds a commonly-cited recent Nepal slab structure — only
    /// when the company has zero slabs configured (never overwrites an
    /// admin's own edits). A starting point to verify against the current
    /// fiscal year's official rates, not a guarantee of them.</summary>
    Task<IReadOnlyList<TaxSlabDto>> SeedDefaultsAsync(CancellationToken ct = default);
}

public interface IPayrollRunService
{
    /// <summary>Creates the Draft run and computes every active employee's line
    /// in the same call — the "Run Payroll" button's one action.</summary>
    Task<PayrollRunDto> CreateAsync(CreatePayrollRunRequest request, CancellationToken ct = default);
    /// <summary>Re-runs the calculation for a still-Draft run, picking up any
    /// attendance/advance changes recorded since it was created.</summary>
    Task<PayrollRunDto> RecomputeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Locks the run and applies each line's AdvanceDeductionAmount to
    /// the employee's advance balance. One-way — a Completed run is final.</summary>
    Task<PayrollRunDto> CompleteAsync(Guid id, CancellationToken ct = default);
    Task<PayrollRunDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollRunDto>> ListAsync(Guid? branchId = null, CancellationToken ct = default);
}
