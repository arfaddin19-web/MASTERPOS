using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

public class ProductGroup : CompanyOwnedEntity
{
    public string Name { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
