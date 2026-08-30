using MasterPOS.Application.Common;
using MasterPOS.Domain.Accounting;
using MasterPOS.Domain.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Accounting;

public class JournalEntryService : IJournalEntryService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public JournalEntryService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<JournalEntryDto> CreateAsync(CreateJournalEntryRequest request, CancellationToken ct = default)
    {
        var branchId = _currentUser.BranchId
            ?? throw new AppException("Your account has no default branch assigned.");

        var entry = new JournalEntry
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = branchId,
            JournalNumber = await GenerateNumberAsync(ct),
            EntryDate = request.EntryDate,
            Narration = request.Narration,
            Status = DocumentStatus.Draft,
        };
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(entry.Id, ct));
    }

    public async Task<JournalEntryDto> AddLineAsync(Guid journalEntryId, AddJournalEntryLineRequest request, CancellationToken ct = default)
    {
        var entry = await GetOwnedAsync(journalEntryId, ct);
        EnsureDraft(entry);
        ValidateLineAmounts(request.DebitAmount, request.CreditAmount);

        if (!await _db.ChartOfAccounts.AnyAsync(a => a.Id == request.AccountId && a.CompanyId == _currentUser.CompanyId && !a.IsDeleted, ct))
            throw new AppException("The selected account does not exist.");

        _db.JournalEntryLines.Add(new JournalEntryLine
        {
            JournalEntryId = entry.Id,
            AccountId = request.AccountId,
            DebitAmount = request.DebitAmount,
            CreditAmount = request.CreditAmount,
            LineNarration = request.LineNarration,
        });
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(journalEntryId, ct));
    }

    public async Task<JournalEntryDto> RemoveLineAsync(Guid journalEntryId, Guid lineId, CancellationToken ct = default)
    {
        var entry = await GetOwnedAsync(journalEntryId, ct);
        EnsureDraft(entry);

        var line = entry.Lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new AppException("Journal entry line not found.");
        _db.JournalEntryLines.Remove(line);
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(journalEntryId, ct));
    }

    public async Task<JournalEntryDto> PostAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await GetOwnedAsync(id, ct);
        EnsureDraft(entry);
        if (entry.Lines.Count < 2)
            throw new AppException("A journal entry needs at least two lines before it can be posted.");

        var totalDebit = entry.Lines.Sum(l => l.DebitAmount);
        var totalCredit = entry.Lines.Sum(l => l.CreditAmount);
        if (totalDebit != totalCredit)
            throw new AppException($"This entry doesn't balance — total debit Rs. {totalDebit:0.00} vs total credit Rs. {totalCredit:0.00}.");

        entry.Status = DocumentStatus.Posted;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Posted", "Accounting.JournalEntries", entry.Id,
            $"posted journal entry {entry.JournalNumber} (Rs. {totalDebit:0.00})", ct);
        return ToDto(entry);
    }

    public async Task<JournalEntryDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await GetOwnedAsync(id, ct);
        if (entry.Status != DocumentStatus.Draft)
            throw new AppException($"Journal entry {entry.JournalNumber} is {entry.Status} and can no longer be cancelled directly.");

        entry.Status = DocumentStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Cancelled", "Accounting.JournalEntries", entry.Id, $"cancelled journal entry {entry.JournalNumber}", ct);
        return ToDto(entry);
    }

    public async Task<JournalEntryDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<JournalEntryDto>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _db.JournalEntries
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .Where(j => j.CompanyId == _currentUser.CompanyId && !j.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DocumentStatus>(status, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown status '{status}'.");
            query = query.Where(j => j.Status == parsed);
        }

        var entries = await query.OrderByDescending(j => j.EntryDate).ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    private static void ValidateLineAmounts(decimal debit, decimal credit)
    {
        // Mirrors CK_JournalEntryLines_OneSided — validated here too so a bad
        // request gets a clear 400 instead of a raw SQL constraint error.
        var oneSided = (debit > 0 && credit == 0) || (credit > 0 && debit == 0);
        if (!oneSided)
            throw new AppException("Exactly one of Debit or Credit must be greater than zero, not both or neither.");
    }

    private static void EnsureDraft(JournalEntry entry)
    {
        if (entry.Status != DocumentStatus.Draft)
            throw new AppException($"Journal entry {entry.JournalNumber} is {entry.Status} and can no longer be changed.");
    }

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        const string prefix = "JE-";
        var companyId = _currentUser.CompanyId;
        var numbers = await _db.JournalEntries
            .Where(j => j.CompanyId == companyId && j.JournalNumber.StartsWith(prefix))
            .Select(j => j.JournalNumber)
            .ToListAsync(ct);

        var next = numbers
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var num) ? num : 0)
            .DefaultIfEmpty(400)
            .Max() + 1;

        return $"{prefix}{next}";
    }

    private async Task<JournalEntry> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var entry = await _db.JournalEntries
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .SingleOrDefaultAsync(j => j.Id == id && j.CompanyId == _currentUser.CompanyId && !j.IsDeleted, ct);
        return entry ?? throw new AppException("Journal entry not found.");
    }

    private static JournalEntryDto ToDto(JournalEntry j) => new(
        j.Id, j.JournalNumber, j.EntryDate, j.Narration, j.Status.ToString(),
        j.Lines.Sum(l => l.DebitAmount), j.Lines.Sum(l => l.CreditAmount),
        j.Lines.Select(l => new JournalEntryLineDto(l.Id, l.AccountId, l.Account.Name, l.DebitAmount, l.CreditAmount, l.LineNarration)).ToList());
}
