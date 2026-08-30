using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Sales;

/// <summary>Audit trail of KOT print/reprint events — one row per print,
/// grouped by station so the kitchen and bar each get only their own
/// items.</summary>
public class KotPrintLog
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public KotStation Station { get; set; }
    public Guid PrintedByUserId { get; set; }
    public DateTime PrintedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsReprint { get; set; }

    public Order Order { get; set; } = null!;
}
