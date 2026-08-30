using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Workforce;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Workforce;

public class EmployeeService : IEmployeeService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public EmployeeService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new AppException("Name is required.");
        if (request.BasicSalary < 0)
            throw new AppException("Basic salary can't be negative.");

        if (!await _db.Branches.AnyAsync(b => b.Id == request.BranchId && b.CompanyId == _currentUser.CompanyId && !b.IsDeleted, ct))
            throw new AppException("The selected branch does not exist.");
        var maritalStatus = ParseMaritalStatus(request.MaritalStatus);

        var employee = new Employee
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = request.BranchId,
            FullName = request.FullName,
            RoleTitle = request.RoleTitle,
            Phone = request.Phone,
            JoinDate = request.JoinDate,
            BasicSalary = request.BasicSalary,
            ShiftStart = request.ShiftStart,
            ShiftEnd = request.ShiftEnd,
            MaritalStatus = maritalStatus,
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(employee.Id, ct));
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        // Unlike Products, an Employee's fields stay editable even with payroll
        // history — a salary raise or a shift change is normal HR business, and
        // every PayrollRunLine already snapshots its own BasicAmount at run time,
        // so past runs can't be silently rewritten by this. Only a name change
        // reaching a completed PayrollRunLine.EmployeeName join is cosmetic.
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new AppException("Name is required.");
        if (request.BasicSalary < 0)
            throw new AppException("Basic salary can't be negative.");

        var maritalStatus = ParseMaritalStatus(request.MaritalStatus);

        var employee = await GetOwnedAsync(id, ct);
        employee.FullName = request.FullName;
        employee.RoleTitle = request.RoleTitle;
        employee.Phone = request.Phone;
        employee.BasicSalary = request.BasicSalary;
        employee.ShiftStart = request.ShiftStart;
        employee.ShiftEnd = request.ShiftEnd;
        employee.MaritalStatus = maritalStatus;
        await _db.SaveChangesAsync(ct);

        return ToDto(employee);
    }

    public async Task<EmployeeDto> SetActiveAsync(Guid id, SetEmployeeActiveRequest request, CancellationToken ct = default)
    {
        var employee = await GetOwnedAsync(id, ct);
        employee.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToDto(employee);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await GetOwnedAsync(id, ct);

        var hasHistory = await _db.Attendances.AnyAsync(a => a.EmployeeId == id, ct)
            || await _db.LeaveRequests.AnyAsync(l => l.EmployeeId == id, ct)
            || await _db.EmployeeAdvances.AnyAsync(a => a.EmployeeId == id, ct)
            || await _db.PayrollRunLines.AnyAsync(l => l.EmployeeId == id, ct);
        if (hasHistory)
            throw new AppException($"'{employee.FullName}' has attendance, leave, advance or payroll history and can't be deleted — deactivate instead.");

        employee.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Deleted", "Workforce.Employees", employee.Id, $"deleted employee '{employee.FullName}'", ct);
    }

    public async Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _db.Employees
            .Include(e => e.Branch)
            .Where(e => e.CompanyId == _currentUser.CompanyId && !e.IsDeleted);
        if (activeOnly) query = query.Where(e => e.IsActive);

        var employees = await query.OrderBy(e => e.FullName).ToListAsync(ct);
        return employees.Select(ToDto).ToList();
    }

    private async Task<Employee> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var employee = await _db.Employees
            .Include(e => e.Branch)
            .SingleOrDefaultAsync(e => e.Id == id && e.CompanyId == _currentUser.CompanyId && !e.IsDeleted, ct);
        return employee ?? throw new AppException("Employee not found.");
    }

    private static MaritalStatus ParseMaritalStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return MaritalStatus.Single;
        if (!Enum.TryParse<MaritalStatus>(value, ignoreCase: true, out var parsed))
            throw new AppException($"Unknown marital status '{value}'.");
        return parsed;
    }

    private static EmployeeDto ToDto(Employee e) => new(
        e.Id, e.BranchId, e.Branch.Name, e.FullName, e.RoleTitle, e.Phone, e.JoinDate,
        e.BasicSalary, e.ShiftStart, e.ShiftEnd, e.MaritalStatus.ToString(), e.IsActive);
}
