using MasterPOS.Domain.Utility;
using MasterPOS.Infrastructure.Persistence;

namespace MasterPOS.Application.Common;

public class AuditLogger : IAuditLogger
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public AuditLogger(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId, string description, CancellationToken ct = default)
    {
        try
        {
            _db.AuditLogEntries.Add(new AuditLogEntry
            {
                CompanyId = _currentUser.CompanyId,
                UserId = _currentUser.UserId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Never let a logging failure take down the business operation
            // that already succeeded — see the interface's class remarks.
        }
    }
}
