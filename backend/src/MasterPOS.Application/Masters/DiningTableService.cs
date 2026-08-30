using MasterPOS.Application.Common;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Masters;

public class DiningTableService : IDiningTableService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public DiningTableService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DiningTableDto> CreateAsync(CreateDiningTableRequest request, CancellationToken ct = default)
    {
        Validate(request.TableNumber, request.Seats);
        if (!await _db.Branches.AnyAsync(b => b.Id == request.BranchId && b.CompanyId == _currentUser.CompanyId && !b.IsDeleted, ct))
            throw new AppException("The selected branch does not exist.");
        if (await _db.DiningTables.AnyAsync(t => t.BranchId == request.BranchId && t.TableNumber == request.TableNumber && !t.IsDeleted, ct))
            throw new AppException($"Table '{request.TableNumber}' already exists for this branch.");

        var table = new DiningTable
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = request.BranchId,
            TableNumber = request.TableNumber,
            FloorLabel = request.FloorLabel,
            Seats = request.Seats,
        };
        _db.DiningTables.Add(table);
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(table.Id, ct));
    }

    public async Task<DiningTableDto> UpdateAsync(Guid id, UpdateDiningTableRequest request, CancellationToken ct = default)
    {
        Validate(request.TableNumber, request.Seats);
        var table = await GetOwnedAsync(id, ct);
        if (table.Status != DiningTableStatus.Vacant)
            throw new AppException($"Table '{table.TableNumber}' is {table.Status} — it can only be edited while Vacant.");

        var duplicate = await _db.DiningTables.AnyAsync(
            t => t.Id != id && t.BranchId == table.BranchId && t.TableNumber == request.TableNumber && !t.IsDeleted, ct);
        if (duplicate)
            throw new AppException($"Table '{request.TableNumber}' already exists for this branch.");

        table.TableNumber = request.TableNumber;
        table.FloorLabel = request.FloorLabel;
        table.Seats = request.Seats;
        await _db.SaveChangesAsync(ct);
        return ToDto(table);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var table = await GetOwnedAsync(id, ct);
        if (table.Status != DiningTableStatus.Vacant)
            throw new AppException($"Table '{table.TableNumber}' is {table.Status} and can't be deleted right now.");
        if (await _db.Orders.AnyAsync(o => o.TableId == id, ct))
            throw new AppException($"Table '{table.TableNumber}' has order history and can't be deleted.");

        table.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DiningTableDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<DiningTableDto>> ListAsync(Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _db.DiningTables
            .Include(t => t.Branch)
            .Where(t => t.CompanyId == _currentUser.CompanyId && !t.IsDeleted);
        if (branchId is { } b) query = query.Where(t => t.BranchId == b);

        var tables = await query.OrderBy(t => t.FloorLabel).ThenBy(t => t.TableNumber).ToListAsync(ct);
        return tables.Select(ToDto).ToList();
    }

    private static void Validate(string tableNumber, int seats)
    {
        if (string.IsNullOrWhiteSpace(tableNumber))
            throw new AppException("Table number is required.");
        if (seats <= 0)
            throw new AppException("Seats must be greater than zero.");
    }

    private async Task<DiningTable> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var table = await _db.DiningTables
            .Include(t => t.Branch)
            .SingleOrDefaultAsync(t => t.Id == id && t.CompanyId == _currentUser.CompanyId && !t.IsDeleted, ct);
        return table ?? throw new AppException("Dining table not found.");
    }

    private static DiningTableDto ToDto(DiningTable t) => new(
        t.Id, t.BranchId, t.Branch.Name, t.TableNumber, t.FloorLabel, t.Seats, t.Status.ToString());
}
