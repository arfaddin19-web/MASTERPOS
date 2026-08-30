using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

/// <summary>Maps to table Masters.Units — named UnitOfMeasure in C# to avoid
/// the generic word "Unit" clashing with test-project connotations.</summary>
public class UnitOfMeasure : CompanyOwnedEntity
{
    public string Name { get; set; } = null!;
    public string? ShortCode { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
