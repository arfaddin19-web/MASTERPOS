using System.Net;
using MasterPOS.Application.Inventory;
using MasterPOS.Application.Masters;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class MastersProductTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    [Fact]
    public async Task Recipe_product_is_rejected_with_zero_BOM_lines()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client, "Plate");
        var recipe = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Veg Thali", productType: "Recipe");

        var response = await Client.PutJsonAsync($"/api/masters/products/{recipe.Id}/bom",
            new SetProductBomRequest([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recipe_BOM_accepts_only_Inventory_type_ingredients_and_never_itself()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var rice = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Rice", productType: "Inventory");
        var service = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Delivery Charge", productType: "Service");
        var recipe = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Veg Thali", productType: "Recipe");

        var serviceIngredient = await Client.PutJsonAsync($"/api/masters/products/{recipe.Id}/bom",
            new SetProductBomRequest([new SetProductBomLine(service.Id, 1)]));
        Assert.Equal(HttpStatusCode.BadRequest, serviceIngredient.StatusCode);

        var selfIngredient = await Client.PutJsonAsync($"/api/masters/products/{recipe.Id}/bom",
            new SetProductBomRequest([new SetProductBomLine(recipe.Id, 1)]));
        Assert.Equal(HttpStatusCode.BadRequest, selfIngredient.StatusCode);

        var validBom = await Client.PutJsonAsync($"/api/masters/products/{recipe.Id}/bom",
            new SetProductBomRequest([new SetProductBomLine(rice.Id, 0.2m)]));
        Assert.Equal(HttpStatusCode.OK, validBom.StatusCode);

        var readBack = await Client.GetJsonAsync<List<ProductBomLineDto>>($"/api/masters/products/{recipe.Id}/bom");
        var line = Assert.Single(readBack!);
        Assert.Equal(rice.Id, line.ComponentProductId);
        Assert.Equal(0.2m, line.Quantity);
    }

    [Fact]
    public async Task Setting_a_BOM_on_a_non_Recipe_product_is_rejected()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var rice = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Rice", productType: "Inventory");
        var otherRice = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Other Rice", productType: "Inventory");

        var response = await Client.PutJsonAsync($"/api/masters/products/{rice.Id}/bom",
            new SetProductBomRequest([new SetProductBomLine(otherRice.Id, 1)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_barcode_is_rejected()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var barcode = ApiTestFixture.Unique("BC");

        var first = await Client.PostJsonAsync("/api/masters/products", new UpsertProductRequest(
            ApiTestFixture.Unique("First"), "Inventory", null, null, unit.Id, null, barcode, 10, 15, true, 0, null, null, true, true));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Client.PostJsonAsync("/api/masters/products", new UpsertProductRequest(
            ApiTestFixture.Unique("Second"), "Inventory", null, null, unit.Id, null, barcode, 10, 15, true, 0, null, null, true, true));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Product_with_transaction_history_can_only_be_deactivated_not_fully_edited_or_deleted()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Lock Test Store");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Lockable Rice");

        // Before any transaction history: full edit and delete both work.
        var editBefore = await Client.PutJsonAsync($"/api/masters/products/{product.Id}", new UpsertProductRequest(
            product.Name, product.ProductType, product.CategoryId, product.GroupId, product.UnitId,
            product.DefaultWarehouseId, product.Barcode, 111, 222, product.IsVatApplicable, product.ReorderLevel,
            product.KotStation, product.PrepTimeMinutes, product.TrackInPos, product.IsActive));
        Assert.Equal(HttpStatusCode.OK, editBefore.StatusCode);

        // Give it transaction history via Opening Stock — the same "an actual
        // stock-moving row exists" trigger the Masters module locks on.
        var openingStock = await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(
            warehouse.Id, product.Id, 10, 100, DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.OK, openingStock.StatusCode);

        var editAfter = await Client.PutJsonAsync($"/api/masters/products/{product.Id}", new UpsertProductRequest(
            product.Name, product.ProductType, product.CategoryId, product.GroupId, product.UnitId,
            product.DefaultWarehouseId, product.Barcode, 333, 444, product.IsVatApplicable, product.ReorderLevel,
            product.KotStation, product.PrepTimeMinutes, product.TrackInPos, product.IsActive));
        Assert.Equal(HttpStatusCode.Conflict, editAfter.StatusCode);

        var deleteAfter = await Client.DeleteAsync($"/api/masters/products/{product.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteAfter.StatusCode);

        var deactivate = await Client.PatchJsonAsync($"/api/masters/products/{product.Id}/active", new SetProductActiveRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.ReadAsAsync<ProductDto>();
        Assert.False(deactivated.IsActive);
        // Price from before the lock kicked in — untouched by the failed edits.
        Assert.Equal(111, deactivated.PurchasePrice);
    }

    [Fact]
    public async Task Deleting_a_nonexistent_product_is_404_not_409()
    {
        var response = await Client.DeleteAsync($"/api/masters/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_product_still_used_as_a_BOM_ingredient_is_409_not_404()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var rice = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Ingredient Rice", productType: "Inventory");
        var recipe = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Uses Rice", productType: "Recipe");
        await Client.PutJsonAsync($"/api/masters/products/{recipe.Id}/bom", new SetProductBomRequest([new SetProductBomLine(rice.Id, 1)]));

        var response = await Client.DeleteAsync($"/api/masters/products/{rice.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
