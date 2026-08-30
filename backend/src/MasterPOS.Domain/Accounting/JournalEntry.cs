using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;

namespace MasterPOS.Domain.Accounting;

public class JournalEntry : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string JournalNumber { get; set; } = null!;
    public DateOnly EntryDate { get; set; }
    public string? Narration { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public Branch Branch { get; set; } = null!;
    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}
