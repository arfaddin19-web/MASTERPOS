using MasterPOS.Application.Common;
using MasterPOS.Domain.Accounting;
using MasterPOS.Domain.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Accounting;

public class OpeningBalanceService : IOpeningBalanceService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public OpeningBalanceService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OpeningBalanceDto> CreateAsync(UpsertOpeningBalanceRequest request, CancellationToken ct = default)
    {
        var balanceType = await ValidateAsync(request, ct);
        var balance = new OpeningBalance
        {
            CompanyId = _currentUser.CompanyId,
            PartyId = request.PartyId,
            AccountId = request.AccountId,
            Amount = request.Amount,
            BalanceType = balanceType,
            AsOfDate = request.AsOfDate,
        };
        _db.OpeningBalances.Add(balance);
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(balance.Id, ct));
    }

    public async Task<OpeningBalanceDto> UpdateAsync(Guid id, UpsertOpeningBalanceRequest request, CancellationToken ct = default)
    {
        var balanceType = await ValidateAsync(request, ct);
        var balance = await GetOwnedAsync(id, ct);
        balance.PartyId = request.PartyId;
        balance.AccountId = request.AccountId;
        balance.Amount = request.Amount;
        balance.BalanceType = balanceType;
        balance.AsOfDate = request.AsOfDate;
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var balance = await GetOwnedAsync(id, ct);
        balance.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OpeningBalanceDto>> ListAsync(CancellationToken ct = default)
    {
        var balances = await _db.OpeningBalances
            .Include(b => b.Party)
            .Include(b => b.Account)
            .Where(b => b.CompanyId == _currentUser.CompanyId && !b.IsDeleted)
            .OrderByDescending(b => b.AsOfDate)
            .ToListAsync(ct);
        return balances.Select(ToDto).ToList();
    }

    private async Task<BalanceType> ValidateAsync(UpsertOpeningBalanceRequest request, CancellationToken ct)
    {
        if ((request.PartyId is null) == (request.AccountId is null))
            throw new AppException("Set exactly one of Party or Account, not both or neither.");
        if (request.Amount <= 0)
            throw new AppException("Amount must be greater than zero.");
        if (!Enum.TryParse<BalanceType>(request.BalanceType, ignoreCase: true, out var balanceType))
            throw new AppException($"Unknown balance type '{request.BalanceType}'.");

        if (request.PartyId is { } partyId
            && !await _db.Parties.AnyAsync(p => p.Id == partyId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct))
            throw new AppException("The selected party does not exist.");
        if (request.AccountId is { } accountId
            && !await _db.ChartOfAccounts.AnyAsync(a => a.Id == accountId && a.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct))
            throw new AppException("The selected account does not exist.");

        return balanceType;
    }

    private async Task<OpeningBalance> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var balance = await _db.OpeningBalances
            .Include(b => b.Party)
            .Include(b => b.Account)
            .SingleOrDefaultAsync(b => b.Id == id && b.CompanyId == _currentUser.CompanyId && !b.IsDeleted, ct);
        return balance ?? throw new AppException("Opening balance not found.");
    }

    private static OpeningBalanceDto ToDto(OpeningBalance b) => new(
        b.Id, b.PartyId, b.Party?.Name, b.AccountId, b.Account?.Name, b.Amount, b.BalanceType.ToString(), b.AsOfDate);
}
