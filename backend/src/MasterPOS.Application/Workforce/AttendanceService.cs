using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Workforce;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Workforce;

public class AttendanceService : IAttendanceService
{
    // How late past ShiftStart still counts as "Present" rather than "Late".
    private static readonly TimeSpan LateGrace = TimeSpan.FromMinutes(15);

    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public AttendanceService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AttendanceDto> CheckInAsync(CheckInRequest request, CancellationToken ct = default)
    {
        var employee = await GetOwnedEmployeeAsync(request.EmployeeId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var existing = await _db.Attendances.SingleOrDefaultAsync(
            a => a.EmployeeId == employee.Id && a.AttendanceDate == today && !a.IsDeleted, ct);
        if (existing is not null)
            throw new AppException($"'{employee.FullName}' is already checked in for today.");

        var status = AttendanceStatus.Present;
        if (employee.ShiftStart is { } shiftStart && TimeOnly.FromDateTime(now) > shiftStart.Add(LateGrace))
            status = AttendanceStatus.Late;

        var attendance = new Attendance
        {
            EmployeeId = employee.Id,
            AttendanceDate = today,
            CheckInAtUtc = now,
            Status = status,
        };
        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(attendance.Id, ct));
    }

    public async Task<AttendanceDto> CheckOutAsync(Guid id, CancellationToken ct = default)
    {
        var attendance = await GetOwnedAsync(id, ct);
        if (attendance.CheckOutAtUtc is not null)
            throw new AppException("Already checked out.");
        if (attendance.CheckInAtUtc is not { } checkIn)
            throw new AppException("There's no check-in recorded to check out from.");

        var now = DateTime.UtcNow;
        attendance.CheckOutAtUtc = now;
        attendance.OvertimeHours = ComputeOvertimeHours(attendance.Employee, checkIn, now);
        await _db.SaveChangesAsync(ct);

        return ToDto(attendance);
    }

    public async Task<AttendanceDto> MarkAsync(MarkAttendanceRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<AttendanceStatus>(request.Status, ignoreCase: true, out var status))
            throw new AppException($"Unknown attendance status '{request.Status}'.");
        if (request.OvertimeHours < 0)
            throw new AppException("Overtime hours can't be negative.");

        var employee = await GetOwnedEmployeeAsync(request.EmployeeId, ct);

        var attendance = await _db.Attendances.SingleOrDefaultAsync(
            a => a.EmployeeId == employee.Id && a.AttendanceDate == request.AttendanceDate && !a.IsDeleted, ct);
        if (attendance is null)
        {
            attendance = new Attendance { EmployeeId = employee.Id, AttendanceDate = request.AttendanceDate };
            _db.Attendances.Add(attendance);
        }

        attendance.Status = status;
        attendance.CheckInAtUtc = request.CheckInAtUtc;
        attendance.CheckOutAtUtc = request.CheckOutAtUtc;
        attendance.OvertimeHours = request.OvertimeHours;
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(attendance.Id, ct));
    }

    public async Task<IReadOnlyList<AttendanceDto>> ListAsync(
        Guid? employeeId = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default)
    {
        var query = _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.Employee.CompanyId == _currentUser.CompanyId && !a.IsDeleted);

        if (employeeId is { } e) query = query.Where(a => a.EmployeeId == e);
        if (fromDate is { } from) query = query.Where(a => a.AttendanceDate >= from);
        if (toDate is { } to) query = query.Where(a => a.AttendanceDate <= to);

        var records = await query.OrderByDescending(a => a.AttendanceDate).ToListAsync(ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TodayAttendanceRowDto>> GetTodaySnapshotAsync(CancellationToken ct = default)
    {
        var branchId = _currentUser.BranchId
            ?? throw new AppException("Your account has no default branch assigned.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var employees = await _db.Employees
            .Where(e => e.CompanyId == _currentUser.CompanyId && e.BranchId == branchId && e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.FullName)
            .ToListAsync(ct);
        if (employees.Count == 0) return Array.Empty<TodayAttendanceRowDto>();

        var employeeIds = employees.Select(e => e.Id).ToList();
        var todaysAttendance = await _db.Attendances
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.AttendanceDate == today && !a.IsDeleted)
            .ToDictionaryAsync(a => a.EmployeeId, ct);
        var onApprovedLeaveToday = (await _db.LeaveRequests
            .Where(l => employeeIds.Contains(l.EmployeeId) && l.Status == LeaveStatus.Approved
                && l.FromDate <= today && l.ToDate >= today && !l.IsDeleted)
            .Select(l => l.EmployeeId)
            .ToListAsync(ct)).ToHashSet();

        return employees.Select(e =>
        {
            if (todaysAttendance.TryGetValue(e.Id, out var a))
                return new TodayAttendanceRowDto(e.Id, e.FullName, e.ShiftStart, e.ShiftEnd,
                    a.CheckInAtUtc, a.CheckOutAtUtc, a.OvertimeHours, a.Status.ToString());

            var status = onApprovedLeaveToday.Contains(e.Id) ? AttendanceStatus.OnLeave.ToString() : null;
            return new TodayAttendanceRowDto(e.Id, e.FullName, e.ShiftStart, e.ShiftEnd, null, null, null, status);
        }).ToList();
    }

    private static decimal ComputeOvertimeHours(Employee employee, DateTime checkInUtc, DateTime checkOutUtc)
    {
        var workedHours = (decimal)(checkOutUtc - checkInUtc).TotalHours;
        if (employee.ShiftStart is not { } start || employee.ShiftEnd is not { } end)
            return 0m;

        var standardHours = (decimal)(end.ToTimeSpan() - start.ToTimeSpan()).TotalHours;
        if (standardHours <= 0) return 0m;

        var overtime = workedHours - standardHours;
        return overtime > 0 ? Math.Round(overtime, 2) : 0m;
    }

    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId, CancellationToken ct)
    {
        var employee = await _db.Employees.SingleOrDefaultAsync(
            e => e.Id == employeeId && e.CompanyId == _currentUser.CompanyId && !e.IsDeleted, ct);
        return employee ?? throw new AppException("The selected employee does not exist.");
    }

    private async Task<Attendance> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var attendance = await _db.Attendances
            .Include(a => a.Employee)
            .SingleOrDefaultAsync(a => a.Id == id && a.Employee.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct);
        return attendance ?? throw new AppException("Attendance record not found.");
    }

    private static AttendanceDto ToDto(Attendance a) => new(
        a.Id, a.EmployeeId, a.Employee.FullName, a.AttendanceDate,
        a.CheckInAtUtc, a.CheckOutAtUtc, a.Status.ToString(), a.OvertimeHours);
}
