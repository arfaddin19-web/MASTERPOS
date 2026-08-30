using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;

namespace MasterPOS.Domain.Masters;

public class Warehouse : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; }

    public Branch Branch { get; set; } = null!;
}
