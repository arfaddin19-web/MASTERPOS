using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Workforce;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Workforce;

public class EmployeeAdvanceService : IEmployeeAdvanceService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public EmployeeAdvanceService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EmployeeAdvanceDto> CreateAsync(CreateEmployeeAdvanceRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new AppException("Amount must be greater than zero.");

        if (!await _db.Employees.AnyAsync(e => e.Id == request.EmployeeId && e.CompanyId == _currentUser.CompanyId && !e.IsDeleted, ct))
            throw new AppException("The selected employee does not exist.");

        var advance = new EmployeeAdvance
        {
            EmployeeId = request.EmployeeId,
            Amount = request.Amount,
            AdvanceDate = request.AdvanceDate,
            Reason = request.Reason,
            Status = AdvanceStatus.Open,
        };
        _db.EmployeeAdvances.Add(advance);
        await _db.SaveChangesAsync(ct);

        return ToDto(await GetOwnedAsync(advance.Id, ct));
    }

    public async Task<EmployeeAdvanceDto> RecordRecoveryAsync(Guid id, RecordAdvanceRecoveryRequest request, CancellationToken ct = default)
    {
        var advance = await GetOwnedAsync(id, ct);
        if (advance.Status == AdvanceStatus.Recovered)
            throw new AppException("This advance is already fully recovered.");
        if (request.Amount <= 0)
            throw new AppException("Recovery amount must be greater than zero.");

        var balance = advance.Amount - advance.AmountRecovered;
        if (request.Amount > balance)
            throw new AppException($"Recovery of Rs. {request.Amount:0.00} exceeds the outstanding balance of Rs. {balance:0.00}.");

        advance.AmountRecovered += request.Amount;
        advance.Status = advance.AmountRecovered >= advance.Amount ? AdvanceStatus.Recovered : AdvanceStatus.PartiallyRecovered;
        await _db.SaveChangesAsync(ct);

        return ToDto(advance);
    }

    public async Task<IReadOnlyList<EmployeeAdvanceDto>> ListAsync(Guid? employeeId = null, string? status = null, CancellationToken ct = default)
    {
        var query = _db.EmployeeAdvances
            .Include(a => a.Employee)
            .Where(a => a.Employee.CompanyId == _currentUser.CompanyId && !a.IsDeleted);

        if (employeeId is { } e) query = query.Where(a => a.EmployeeId == e);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AdvanceStatus>(status, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown status '{status}'.");
            query = query.Where(a => a.Status == parsed);
        }

        var advances = await query.OrderByDescending(a => a.AdvanceDate).ToListAsync(ct);
        return advances.Select(ToDto).ToList();
    }

    private async Task<EmployeeAdvance> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var advance = await _db.EmployeeAdvances
            .Include(a => a.Employee)
            .SingleOrDefaultAsync(a => a.Id == id && a.Employee.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct);
        return advance ?? throw new AppException("Employee advance not found.");
    }

    private static EmployeeAdvanceDto ToDto(EmployeeAdvance a) => new(
        a.Id, a.EmployeeId, a.Employee.FullName, a.Amount, a.AdvanceDate, a.Reason,
        a.AmountRecovered, a.Amount - a.AmountRecovered, a.Status.ToString());
}
