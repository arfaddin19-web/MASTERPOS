using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Accounting;

/// <summary>Exactly one of DebitAmount/CreditAmount is non-zero — enforced
/// by CK_JournalEntryLines_OneSided in the database, and worth validating
/// in the application layer too before it ever reaches SQL.</summary>
public class JournalEntryLine
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? LineNarration { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;
    public ChartOfAccount Account { get; set; } = null!;
}
