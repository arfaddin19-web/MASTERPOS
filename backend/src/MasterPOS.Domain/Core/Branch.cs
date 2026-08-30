using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Core;

public class Branch : CompanyOwnedEntity
{
    public string Name { get; set; } = null!;
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
}
