namespace MasterPOS.Domain.Common;

/// <summary>
/// Every table in the schema carries these five columns. Inherit this instead
/// of repeating them — matches the convention documented in the database
/// README (00_README.md in the database delivery).
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Every business table also carries CompanyId — the single-tenant-today,
/// SaaS-ready-tomorrow column. See the database README for why.
/// </summary>
public abstract class CompanyOwnedEntity : AuditableEntity
{
    public Guid CompanyId { get; set; }
}
