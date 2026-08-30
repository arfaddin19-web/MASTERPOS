namespace MasterPOS.Application.Common;

/// <summary>
/// Writes to Settings → Audit Trail (`Utility.AuditLog`). Called from the
/// business-significant moments across modules — a document actually
/// posted/completed/cancelled, a deletion, an account created or
/// deactivated — not from every read or minor field edit; see each call
/// site for what counts. Failing to write an audit entry must never fail
/// the underlying operation, so callers fire this after their own
/// SaveChanges succeeds, and a logging failure here is swallowed, not
/// rethrown — a missing audit row is far less harmful than losing a
/// legitimate business transaction over a logging hiccup.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, Guid? entityId, string description, CancellationToken ct = default);
}
