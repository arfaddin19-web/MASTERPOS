using MasterPOS.Application.Masters;

namespace MasterPOS.Tests.Testing;

/// <summary>Small, reused-everywhere setup steps (a unit, a category, a
/// warehouse, a plain product) that most test classes need before they can
/// exercise the business rule they're actually about — factored out so
/// e.g. every Sales/Purchase/Inventory test isn't re-deriving "how do I get
/// a sellable product" from scratch.</summary>
public static class MastersTestHelpers
{
    public static async Task<UnitDto> CreateUnitAsync(HttpClient client, string name = "Kilogram", string? shortCode = "kg")
    {
        var response = await client.PostJsonAsync("/api/masters/units", new CreateUnitRequest(ApiTestFixture.Unique(name), shortCode));
        return await response.ReadAsAsync<UnitDto>();
    }

    public static async Task<ProductCategoryDto> CreateCategoryAsync(HttpClient client, string name = "Groceries")
    {
        var response = await client.PostJsonAsync("/api/masters/categories", new CreateProductCategoryRequest(ApiTestFixture.Unique(name), null));
        return await response.ReadAsAsync<ProductCategoryDto>();
    }

    public static async Task<WarehouseDto> CreateWarehouseAsync(HttpClient client, Guid branchId, string name = "Store", bool isDefault = false)
    {
        var response = await client.PostJsonAsync("/api/masters/warehouses", new CreateWarehouseRequest(ApiTestFixture.Unique(name), branchId, isDefault));
        return await response.ReadAsAsync<WarehouseDto>();
    }

    public static async Task<ProductDto> CreateProductAsync(
        HttpClient client,
        Guid unitId,
        string name = "Product",
        string productType = "Inventory",
        Guid? categoryId = null,
        Guid? defaultWarehouseId = null,
        decimal purchasePrice = 100m,
        decimal salePrice = 150m,
        bool isVatApplicable = true,
        decimal reorderLevel = 0m,
        bool trackInPos = true)
    {
        var response = await client.PostJsonAsync("/api/masters/products", new UpsertProductRequest(
            Name: ApiTestFixture.Unique(name),
            ProductType: productType,
            CategoryId: categoryId,
            GroupId: null,
            UnitId: unitId,
            DefaultWarehouseId: defaultWarehouseId,
            Barcode: null,
            PurchasePrice: purchasePrice,
            SalePrice: salePrice,
            IsVatApplicable: isVatApplicable,
            ReorderLevel: reorderLevel,
            KotStation: null,
            PrepTimeMinutes: null,
            TrackInPos: trackInPos,
            IsActive: true));
        return await response.ReadAsAsync<ProductDto>();
    }

    public static async Task<PartyDto> CreatePartyAsync(HttpClient client, string partyType = "Supplier", string name = "Party")
    {
        var response = await client.PostJsonAsync("/api/masters/parties", new UpsertPartyRequest(
            PartyType: partyType,
            Name: ApiTestFixture.Unique(name),
            Phone: null,
            Email: null,
            Address: null,
            VatOrPanNumber: null,
            OpeningBalanceAmount: 0,
            OpeningBalanceType: "Dr"));
        return await response.ReadAsAsync<PartyDto>();
    }
}
