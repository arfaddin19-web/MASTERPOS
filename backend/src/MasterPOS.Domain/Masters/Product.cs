using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

/// <summary>
/// Barcode and KotStation/PrepTimeMinutes both exist regardless of the
/// company's BusinessType — the app shows/uses whichever set applies
/// (Trading: Barcode; Cafe: KotStation/PrepTimeMinutes). See the database
/// README for the reasoning.
/// </summary>
public class Product : CompanyOwnedEntity
{
    public Guid? CategoryId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid UnitId { get; set; }
    public Guid? DefaultWarehouseId { get; set; }

    public string Name { get; set; } = null!;
    public ProductType ProductType { get; set; } = ProductType.Inventory;
    public string? Barcode { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public bool IsVatApplicable { get; set; } = true;
    public decimal ReorderLevel { get; set; }
    public KotStation? KotStation { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public string? ImagePath { get; set; }
    public bool TrackInPos { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ProductCategory? Category { get; set; }
    public ProductGroup? Group { get; set; }
    public UnitOfMeasure Unit { get; set; } = null!;
    public Warehouse? DefaultWarehouse { get; set; }

    /// <summary>This Recipe product's BOM — its list of ingredients (populated when ProductType == Recipe).</summary>
    public ICollection<ProductBom> BomComponents { get; set; } = new List<ProductBom>();

    /// <summary>The Recipe(s) this product is used as an ingredient in.</summary>
    public ICollection<ProductBom> UsedInRecipes { get; set; } = new List<ProductBom>();
}
