namespace MasterPOS.Application.Accounting;

// ---- Chart of Accounts ----

public record ChartOfAccountDto(
    Guid Id, string Name, string AccountType, Guid? ParentAccountId, string? ParentAccountName, bool IsSystemAccount);

public record UpsertChartOfAccountRequest(string Name, string AccountType, Guid? ParentAccountId);

// ---- Journal Entries ----

public record JournalEntryLineDto(
    Guid Id, Guid AccountId, string AccountName, decimal DebitAmount, decimal CreditAmount, string? LineNarration);

public record AddJournalEntryLineRequest(Guid AccountId, decimal DebitAmount, decimal CreditAmount, string? LineNarration);

public record JournalEntryDto(
    Guid Id, string JournalNumber, DateOnly EntryDate, string? Narration, string Status,
    decimal TotalDebit, decimal TotalCredit, IReadOnlyList<JournalEntryLineDto> Lines);

public record CreateJournalEntryRequest(DateOnly EntryDate, string? Narration);

// ---- Party Payments ----

public record PartyPaymentDto(
    Guid Id, Guid PartyId, string PartyName, string Direction, decimal Amount, string PaymentMode,
    string? ReferenceType, Guid? ReferenceId, DateOnly PaymentDate, string? Narration);

public record CreatePartyPaymentRequest(
    Guid PartyId, string Direction, decimal Amount, string PaymentMode,
    string? ReferenceType, Guid? ReferenceId, DateOnly PaymentDate, string? Narration);

// ---- Opening Balances ----

public record OpeningBalanceDto(
    Guid Id, Guid? PartyId, string? PartyName, Guid? AccountId, string? AccountName,
    decimal Amount, string BalanceType, DateOnly AsOfDate);

public record UpsertOpeningBalanceRequest(Guid? PartyId, Guid? AccountId, decimal Amount, string BalanceType, DateOnly AsOfDate);
