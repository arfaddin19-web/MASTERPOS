using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;

namespace MasterPOS.Domain.Auth;

public class Role : CompanyOwnedEntity
{
    public string Name { get; set; } = null!;
    public bool IsSystemRole { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
