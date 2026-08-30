using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

public class ProductCategory : CompanyOwnedEntity
{
    public string Name { get; set; } = null!;
    public Guid? ParentCategoryId { get; set; }

    public ProductCategory? ParentCategory { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
