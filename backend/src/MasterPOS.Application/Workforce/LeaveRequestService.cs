using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Workforce;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Workforce;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public LeaveRequestService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.LeaveType))
            throw new AppException("Leave type is required.");
        if (request.ToDate < request.FromDate)
            throw new AppException("To date can't be before the from date.");

        if (!await _db.Employees.AnyAsync(e => e.Id == request.EmployeeId && e.CompanyId == _currentUser.CompanyId && !e.IsDeleted, ct))
            throw new AppException("The selected employee does not exist.");

        var leave = new LeaveRequest
        {
            EmployeeId = request.EmployeeId,
            LeaveType = request.LeaveType,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Reason = request.Reason,
            Status = LeaveStatus.Pending,
        };
        _db.LeaveRequests.Add(leave);
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(leave.Id, ct));
    }

    public async Task<LeaveRequestDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var leave = await GetOwnedAsync(id, ct);
        EnsurePending(leave);
        leave.Status = LeaveStatus.Approved;
        leave.ApprovedByUserId = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToDto(leave);
    }

    public async Task<LeaveRequestDto> RejectAsync(Guid id, CancellationToken ct = default)
    {
        var leave = await GetOwnedAsync(id, ct);
        EnsurePending(leave);
        leave.Status = LeaveStatus.Rejected;
        leave.ApprovedByUserId = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToDto(leave);
    }

    public async Task<LeaveRequestDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var leave = await GetOwnedAsync(id, ct);
        EnsurePending(leave);
        leave.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ToDto(leave);
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> ListAsync(Guid? employeeId = null, string? status = null, CancellationToken ct = default)
    {
        var query = _db.LeaveRequests
            .Include(l => l.Employee)
            .Where(l => l.Employee.CompanyId == _currentUser.CompanyId && !l.IsDeleted);

        if (employeeId is { } e) query = query.Where(l => l.EmployeeId == e);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LeaveStatus>(status, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown status '{status}'.");
            query = query.Where(l => l.Status == parsed);
        }

        var leaves = await query.OrderByDescending(l => l.FromDate).ToListAsync(ct);
        return leaves.Select(ToDto).ToList();
    }

    private static void EnsurePending(LeaveRequest leave)
    {
        if (leave.Status != LeaveStatus.Pending)
            throw new AppException($"This leave request is already {leave.Status}.");
    }

    private async Task<LeaveRequest> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var leave = await _db.LeaveRequests
            .Include(l => l.Employee)
            .SingleOrDefaultAsync(l => l.Id == id && l.Employee.CompanyId == _currentUser.CompanyId && !l.IsDeleted, ct);
        return leave ?? throw new AppException("Leave request not found.");
    }

    private static LeaveRequestDto ToDto(LeaveRequest l) => new(
        l.Id, l.EmployeeId, l.Employee.FullName, l.LeaveType,
        l.FromDate, l.ToDate, l.Status.ToString(), l.ApprovedByUserId, l.Reason);
}
