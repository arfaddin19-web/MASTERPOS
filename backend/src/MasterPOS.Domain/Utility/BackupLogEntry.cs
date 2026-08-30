using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Utility;

public class BackupLogEntry
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime BackupAtUtc { get; set; } = DateTime.UtcNow;
    public string FilePath { get; set; } = null!;
    public long? SizeBytes { get; set; }

    /// <summary>NULL = automatic/scheduled backup.</summary>
    public Guid? TriggeredByUserId { get; set; }
    public BackupStatus Status { get; set; } = BackupStatus.Success;
}
