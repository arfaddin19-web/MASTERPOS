using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

/// <summary>Recipe/composite items — selling a FinishedProduct deducts each
/// ComponentProduct from stock. No CompanyId (scoped via FinishedProduct).</summary>
public class ProductBom : AuditableEntity
{
    public Guid FinishedProductId { get; set; }
    public Guid ComponentProductId { get; set; }
    public decimal Quantity { get; set; }

    public Product FinishedProduct { get; set; } = null!;
    public Product ComponentProduct { get; set; } = null!;
}
