using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Accounting;

public class ChartOfAccountService : IChartOfAccountService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ChartOfAccountService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ChartOfAccountDto> CreateAsync(UpsertChartOfAccountRequest request, CancellationToken ct = default)
    {
        var accountType = await ValidateAsync(request, id: null, ct);
        var account = new ChartOfAccount
        {
            CompanyId = _currentUser.CompanyId,
            Name = request.Name,
            AccountType = accountType,
            ParentAccountId = request.ParentAccountId,
        };
        _db.ChartOfAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(account.Id, ct));
    }

    public async Task<ChartOfAccountDto> UpdateAsync(Guid id, UpsertChartOfAccountRequest request, CancellationToken ct = default)
    {
        var accountType = await ValidateAsync(request, id, ct);
        var account = await GetOwnedAsync(id, ct);
        if (account.IsSystemAccount)
            throw new AppException($"'{account.Name}' is a system account and can't be edited.");

        account.Name = request.Name;
        account.AccountType = accountType;
        account.ParentAccountId = request.ParentAccountId;
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var account = await GetOwnedAsync(id, ct);
        if (account.IsSystemAccount)
            throw new AppException($"'{account.Name}' is a system account and can't be deleted.");

        var inUse = await _db.JournalEntryLines.AnyAsync(l => l.AccountId == id, ct)
            || await _db.OpeningBalances.AnyAsync(o => o.AccountId == id, ct)
            || await _db.ChartOfAccounts.AnyAsync(a => a.ParentAccountId == id && !a.IsDeleted, ct);
        if (inUse)
            throw new AppException($"'{account.Name}' is in use (journal entries, opening balances, or child accounts) and can't be deleted.");

        account.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChartOfAccountDto>> ListAsync(CancellationToken ct = default)
    {
        var accounts = await _db.ChartOfAccounts
            .Include(a => a.ParentAccount)
            .Where(a => a.CompanyId == _currentUser.CompanyId && !a.IsDeleted)
            .OrderBy(a => a.AccountType).ThenBy(a => a.Name)
            .ToListAsync(ct);
        return accounts.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ChartOfAccountDto>> SeedDefaultsAsync(CancellationToken ct = default)
    {
        if (await _db.ChartOfAccounts.AnyAsync(a => a.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct))
            throw new AppException("Chart of Accounts already has entries — delete them first if you want to reseed the defaults.");

        var defaults = new (string Name, AccountType Type)[]
        {
            ("Cash", AccountType.Asset),
            ("Bank", AccountType.Asset),
            ("Accounts Receivable", AccountType.Asset),
            ("Accounts Payable", AccountType.Liability),
            ("VAT Payable", AccountType.Liability),
            ("Opening Balance Equity", AccountType.Equity),
            ("Sales Revenue", AccountType.Income),
            ("Purchases / COGS", AccountType.Expense),
        };
        foreach (var (name, type) in defaults)
            _db.ChartOfAccounts.Add(new ChartOfAccount
            {
                CompanyId = _currentUser.CompanyId, Name = name, AccountType = type, IsSystemAccount = true,
            });
        await _db.SaveChangesAsync(ct);

        return await ListAsync(ct);
    }

    private async Task<AccountType> ValidateAsync(UpsertChartOfAccountRequest request, Guid? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Name is required.");
        if (!Enum.TryParse<AccountType>(request.AccountType, ignoreCase: true, out var accountType))
            throw new AppException($"Unknown account type '{request.AccountType}'.");
        if (request.ParentAccountId is { } parentId)
        {
            if (parentId == id)
                throw new AppException("An account can't be its own parent.");
            if (!await _db.ChartOfAccounts.AnyAsync(a => a.Id == parentId && a.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct))
                throw new AppException("The selected parent account does not exist.");
        }
        return accountType;
    }

    private async Task<ChartOfAccount> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var account = await _db.ChartOfAccounts
            .Include(a => a.ParentAccount)
            .SingleOrDefaultAsync(a => a.Id == id && a.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct);
        return account ?? throw new AppException("Account not found.");
    }

    private static ChartOfAccountDto ToDto(ChartOfAccount a) => new(
        a.Id, a.Name, a.AccountType.ToString(), a.ParentAccountId, a.ParentAccount?.Name, a.IsSystemAccount);
}
