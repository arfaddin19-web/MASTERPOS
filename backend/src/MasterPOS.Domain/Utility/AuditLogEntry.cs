namespace MasterPOS.Domain.Utility;

/// <summary>
/// Backs the Settings → Audit Trail screen. Written by the application
/// layer only (never a DB-trigger audit) — keeps it readable ("Sneha Naik
/// posted Journal Entry #JE-0442") instead of raw column diffs.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Created, Updated, Deleted, Posted, Approved, ...</summary>
    public string Action { get; set; } = null!;

    /// <summary>"Accounting.JournalEntries", "Masters.Products", ...</summary>
    public string EntityType { get; set; } = null!;
    public Guid? EntityId { get; set; }

    /// <summary>"posted Journal Entry #JE-0442"</summary>
    public string Description { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
