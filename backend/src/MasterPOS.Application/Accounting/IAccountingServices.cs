namespace MasterPOS.Application.Accounting;

public interface IChartOfAccountService
{
    Task<ChartOfAccountDto> CreateAsync(UpsertChartOfAccountRequest request, CancellationToken ct = default);
    Task<ChartOfAccountDto> UpdateAsync(Guid id, UpsertChartOfAccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ChartOfAccountDto>> ListAsync(CancellationToken ct = default);
    /// <summary>Seeds a standard minimal chart (Cash, Bank, Accounts
    /// Receivable/Payable, Sales, Purchase, VAT Payable, Opening Balance
    /// Equity), marked IsSystemAccount — only when the company has none yet.</summary>
    Task<IReadOnlyList<ChartOfAccountDto>> SeedDefaultsAsync(CancellationToken ct = default);
}

/// <summary>Manual double-entry bookkeeping. Draft while lines are being
/// built; Post is the one-way step that requires total debits to equal
/// total credits — the fundamental double-entry rule, checked here since
/// no single-row CHECK constraint can enforce it across a whole entry.</summary>
public interface IJournalEntryService
{
    Task<JournalEntryDto> CreateAsync(CreateJournalEntryRequest request, CancellationToken ct = default);
    Task<JournalEntryDto> AddLineAsync(Guid journalEntryId, AddJournalEntryLineRequest request, CancellationToken ct = default);
    Task<JournalEntryDto> RemoveLineAsync(Guid journalEntryId, Guid lineId, CancellationToken ct = default);
    Task<JournalEntryDto> PostAsync(Guid id, CancellationToken ct = default);
    Task<JournalEntryDto> CancelAsync(Guid id, CancellationToken ct = default);
    Task<JournalEntryDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntryDto>> ListAsync(string? status = null, CancellationToken ct = default);
}

/// <summary>The "Payment Entry" transaction — settling a party's balance
/// independent of a specific order. Immutable once recorded, like
/// StockLedgerEntry/OrderPayment — no update/delete, only Create/Get/List.
/// A PurchaseInvoice reference also advances that invoice's own AmountPaid,
/// so the two views of "how much has been paid" never drift apart.</summary>
public interface IPartyPaymentService
{
    Task<PartyPaymentDto> CreateAsync(CreatePartyPaymentRequest request, CancellationToken ct = default);
    Task<PartyPaymentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PartyPaymentDto>> ListAsync(Guid? partyId = null, CancellationToken ct = default);
}

public interface IOpeningBalanceService
{
    Task<OpeningBalanceDto> CreateAsync(UpsertOpeningBalanceRequest request, CancellationToken ct = default);
    Task<OpeningBalanceDto> UpdateAsync(Guid id, UpsertOpeningBalanceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OpeningBalanceDto>> ListAsync(CancellationToken ct = default);
}
