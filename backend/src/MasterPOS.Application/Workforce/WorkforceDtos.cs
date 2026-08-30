namespace MasterPOS.Application.Workforce;

// ---- Employee ----

public record EmployeeDto(
    Guid Id, Guid BranchId, string BranchName,
    string FullName, string? RoleTitle, string? Phone, DateOnly JoinDate,
    decimal BasicSalary, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, string MaritalStatus, bool IsActive);

public record CreateEmployeeRequest(
    Guid BranchId, string FullName, string? RoleTitle, string? Phone, DateOnly JoinDate,
    decimal BasicSalary, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, string? MaritalStatus);

public record UpdateEmployeeRequest(
    string FullName, string? RoleTitle, string? Phone,
    decimal BasicSalary, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, string MaritalStatus);

public record SetEmployeeActiveRequest(bool IsActive);

// ---- Attendance ----

public record AttendanceDto(
    Guid Id, Guid EmployeeId, string EmployeeName, DateOnly AttendanceDate,
    DateTime? CheckInAtUtc, DateTime? CheckOutAtUtc, string Status, decimal OvertimeHours);

public record CheckInRequest(Guid EmployeeId);

public record MarkAttendanceRequest(
    Guid EmployeeId, DateOnly AttendanceDate, string Status,
    DateTime? CheckInAtUtc, DateTime? CheckOutAtUtc, decimal OvertimeHours);

/// <summary>One row of the "Today's Attendance Snapshot" — every active employee in
/// the caller's branch, left-joined with today's Attendance row where one exists.
/// Status is null when the employee hasn't been marked yet today and isn't on
/// approved leave either — the UI shows that as "—", not as "Absent".</summary>
public record TodayAttendanceRowDto(
    Guid EmployeeId, string EmployeeName, TimeOnly? ShiftStart, TimeOnly? ShiftEnd,
    DateTime? CheckInAtUtc, DateTime? CheckOutAtUtc, decimal? OvertimeHours, string? Status);

// ---- Leave ----

public record LeaveRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string LeaveType,
    DateOnly FromDate, DateOnly ToDate, string Status, Guid? ApprovedByUserId, string? Reason);

public record CreateLeaveRequestRequest(Guid EmployeeId, string LeaveType, DateOnly FromDate, DateOnly ToDate, string? Reason);

// ---- Employee Advance ----

public record EmployeeAdvanceDto(
    Guid Id, Guid EmployeeId, string EmployeeName, decimal Amount, DateOnly AdvanceDate,
    string? Reason, decimal AmountRecovered, decimal Balance, string Status);

public record CreateEmployeeAdvanceRequest(Guid EmployeeId, decimal Amount, DateOnly AdvanceDate, string? Reason);

public record RecordAdvanceRecoveryRequest(decimal Amount);

// ---- Payroll Settings ----

public record PayrollSettingsDto(
    bool OvertimeEnabled, decimal OvertimeMultiplier,
    bool PfEnabled, decimal PfEmployeePercent, decimal PfEmployerPercent,
    bool SsfEnabled, decimal SsfEmployeePercent, decimal SsfEmployerPercent,
    bool TdsEnabled,
    bool FestivalBonusEnabled, decimal FestivalBonusPercent);

public record UpdatePayrollSettingsRequest(
    bool OvertimeEnabled, decimal OvertimeMultiplier,
    bool PfEnabled, decimal PfEmployeePercent, decimal PfEmployerPercent,
    bool SsfEnabled, decimal SsfEmployeePercent, decimal SsfEmployerPercent,
    bool TdsEnabled,
    bool FestivalBonusEnabled, decimal FestivalBonusPercent);

// ---- Tax Slabs ----

public record TaxSlabDto(Guid Id, string MaritalStatus, decimal LowerBound, decimal? UpperBound, decimal RatePercent);

public record UpsertTaxSlabRequest(string MaritalStatus, decimal LowerBound, decimal? UpperBound, decimal RatePercent);

// ---- Payroll Run ----

public record PayrollRunLineDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string? RoleTitle,
    decimal BasicAmount, decimal AllowancesAmount, decimal OvertimeAmount, decimal DeductionsAmount,
    decimal PfEmployeeAmount, decimal PfEmployerAmount, decimal SsfEmployeeAmount, decimal SsfEmployerAmount,
    decimal TdsAmount, decimal AdvanceDeductionAmount, decimal NetPayAmount, string LineStatus);

public record PayrollRunDto(
    Guid Id, Guid BranchId, string BranchName, byte PeriodMonth, short PeriodYear, string RunType,
    string Status, DateTime? RunAtUtc, decimal GrossPayroll, decimal NetPayroll,
    IReadOnlyList<PayrollRunLineDto> Lines);

public record CreatePayrollRunRequest(Guid BranchId, byte PeriodMonth, short PeriodYear, string? RunType);
