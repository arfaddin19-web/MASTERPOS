using System.Net;
using MasterPOS.Application.Inventory;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class InventoryTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    [Fact]
    public async Task Opening_stock_rejects_a_second_call_for_the_same_product_and_warehouse()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Opening Dup Store");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Opening Dup Item", defaultWarehouseId: warehouse.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var first = await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, product.Id, 50, 100, today));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, product.Id, 10, 100, today));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Zero_quantity_adjustment_is_rejected()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Zero Adj Store");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Zero Adj Item", defaultWarehouseId: warehouse.Id);

        var response = await Client.PostJsonAsync("/api/inventory/adjustments", new CreateStockAdjustmentRequest(
            warehouse.Id, product.Id, 0, "Should be rejected", DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_stays_Pending_until_posted_and_the_ledger_reflects_it_only_after_posting()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var from = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Transfer From");
        var to = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Transfer To");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Transfer Item", defaultWarehouseId: from.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(from.Id, product.Id, 50, 100, today));

        var transfer = await (await Client.PostJsonAsync("/api/inventory/transfers",
            new CreateStockTransferRequest(product.Id, from.Id, to.Id, 10, today))).ReadAsAsync<StockTransferDto>();
        Assert.Equal("Pending", transfer.Status);

        var balancesBeforePost = await Client.GetJsonAsync<List<StockBalanceDto>>($"/api/inventory/reports/balances?warehouseId={to.Id}");
        Assert.DoesNotContain(balancesBeforePost!, b => b.ProductId == product.Id);

        var posted = await (await Client.PostJsonAsync($"/api/inventory/transfers/{transfer.Id}/post", new { })).ReadAsAsync<StockTransferDto>();
        Assert.Equal("Completed", posted.Status);

        var toBalance = await Client.GetJsonAsync<List<StockBalanceDto>>($"/api/inventory/reports/balances?warehouseId={to.Id}");
        Assert.Equal(10, Assert.Single(toBalance!, b => b.ProductId == product.Id).Balance);

        var fromBalance = await Client.GetJsonAsync<List<StockBalanceDto>>($"/api/inventory/reports/balances?warehouseId={from.Id}");
        Assert.Equal(40, Assert.Single(fromBalance!, b => b.ProductId == product.Id).Balance);

        // A Completed transfer can't be posted again.
        var repost = await Client.PostJsonAsync($"/api/inventory/transfers/{transfer.Id}/post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, repost.StatusCode);
    }

    [Fact]
    public async Task Transfer_exceeding_available_stock_is_accepted_as_Pending_but_rejected_on_post()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var from = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Oversized From");
        var to = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Oversized To");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Oversized Item", defaultWarehouseId: from.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(from.Id, product.Id, 5, 100, today));

        var transfer = await (await Client.PostJsonAsync("/api/inventory/transfers",
            new CreateStockTransferRequest(product.Id, from.Id, to.Id, 1000, today))).ReadAsAsync<StockTransferDto>();
        Assert.Equal("Pending", transfer.Status);

        var post = await Client.PostJsonAsync($"/api/inventory/transfers/{transfer.Id}/post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task Ledger_running_balance_matches_a_hand_computed_sum_of_in_minus_out()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Running Balance Store");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Running Balance Item", defaultWarehouseId: warehouse.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, product.Id, 50, 100, today));
        await Client.PostJsonAsync("/api/inventory/adjustments", new CreateStockAdjustmentRequest(warehouse.Id, product.Id, -2, "Breakage", today));
        await Client.PostJsonAsync("/api/inventory/adjustments", new CreateStockAdjustmentRequest(warehouse.Id, product.Id, 10, "Recount", today));

        var ledger = await Client.GetJsonAsync<List<StockLedgerEntryDto>>($"/api/inventory/reports/ledger?productId={product.Id}&warehouseId={warehouse.Id}");
        var expectedBalance = ledger!.Sum(e => e.QuantityIn) - ledger.Sum(e => e.QuantityOut);
        var lastRunningBalance = ledger.OrderBy(e => e.MovementDate).Last().RunningBalance;

        Assert.Equal(58, expectedBalance); // 50 - 2 + 10
        Assert.Equal(expectedBalance, lastRunningBalance);
    }
}
