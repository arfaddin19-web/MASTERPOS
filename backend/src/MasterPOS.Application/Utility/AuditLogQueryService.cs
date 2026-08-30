using MasterPOS.Application.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Utility;

public class AuditLogQueryService : IAuditLogQueryService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public AuditLogQueryService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> ListAsync(
        DateOnly? fromDate = null, DateOnly? toDate = null, string? entityType = null, CancellationToken ct = default)
    {
        var query = _db.AuditLogEntries.Where(a => a.CompanyId == _currentUser.CompanyId);
        if (fromDate is { } from)
            query = query.Where(a => a.OccurredAtUtc >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (toDate is { } to)
            query = query.Where(a => a.OccurredAtUtc < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        var entries = await query.OrderByDescending(a => a.OccurredAtUtc).Take(500).ToListAsync(ct);
        return entries.Select(a => new AuditLogEntryDto(
            a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.Description, a.OccurredAtUtc)).ToList();
    }
}
