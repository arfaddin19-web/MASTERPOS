using System.Net;
using MasterPOS.Application.Masters;
using MasterPOS.Application.Sales;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class DiningTableAndDiscountOfferTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    [Fact]
    public async Task Occupied_table_rejects_edit_and_delete_then_allows_both_again_once_vacant()
    {
        var table = await (await Client.PostJsonAsync("/api/masters/tables",
            new CreateDiningTableRequest(fixture.BranchId, ApiTestFixture.Unique("T"), "Ground Floor", 4))).ReadAsAsync<DiningTableDto>();
        Assert.Equal("Vacant", table.Status);

        var order = await (await Client.PostJsonAsync("/api/sales/orders",
            new CreateOrderRequest("DineIn", table.Id, 2, null))).ReadAsAsync<OrderDto>();

        var occupied = await Client.GetJsonAsync<DiningTableDto>($"/api/masters/tables/{table.Id}");
        Assert.Equal("Occupied", occupied!.Status);

        var editWhileOccupied = await Client.PutJsonAsync($"/api/masters/tables/{table.Id}",
            new UpdateDiningTableRequest(table.TableNumber, table.FloorLabel, 6));
        Assert.Equal(HttpStatusCode.BadRequest, editWhileOccupied.StatusCode);

        var deleteWhileOccupied = await Client.DeleteAsync($"/api/masters/tables/{table.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteWhileOccupied.StatusCode);

        // Close the order out (no lines, nothing owed) — table should vacate.
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/cancel", new { });
        var vacated = await Client.GetJsonAsync<DiningTableDto>($"/api/masters/tables/{table.Id}");
        Assert.Equal("Vacant", vacated!.Status);

        var editWhileVacant = await Client.PutJsonAsync($"/api/masters/tables/{table.Id}",
            new UpdateDiningTableRequest(table.TableNumber, table.FloorLabel, 6));
        Assert.Equal(HttpStatusCode.OK, editWhileVacant.StatusCode);
    }

    [Fact]
    public async Task Expired_discount_offer_is_rejected_when_applied()
    {
        var expired = await (await Client.PostJsonAsync("/api/sales/discount-offers", new UpsertDiscountOfferRequest(
            ApiTestFixture.Unique("Expired Offer"), "Percent", 10,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))))).ReadAsAsync<DiscountOfferDto>();

        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Expired Offer Item", isVatApplicable: false);
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(product.Id, 1, null));

        var response = await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/discount/offer", new ApplyDiscountOfferRequest(expired.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_flat_discount_larger_than_the_bill_is_capped_at_the_subtotal_not_negative()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Cap Test Item", salePrice: 800, isVatApplicable: true);
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(product.Id, 1, null));

        var discounted = await (await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/discount/manual",
            new ApplyManualDiscountRequest("Amount", 1000))).ReadAsAsync<OrderDto>();

        Assert.Equal(800m, discounted.DiscountAmount); // capped at the Rs.800 subtotal, not the full Rs.1000
        Assert.Equal(0m, discounted.VatAmount);
        Assert.Equal(0m, discounted.GrandTotalAmount);
    }

    [Fact]
    public async Task A_percent_discount_over_100_is_rejected_both_at_offer_creation_and_manual_apply()
    {
        var invalidOffer = await Client.PostJsonAsync("/api/sales/discount-offers",
            new UpsertDiscountOfferRequest(ApiTestFixture.Unique("Over 100"), "Percent", 150, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidOffer.StatusCode);

        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Over 100 Item");
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(product.Id, 1, null));

        var invalidManual = await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/discount/manual",
            new ApplyManualDiscountRequest("Percent", 150));
        Assert.Equal(HttpStatusCode.BadRequest, invalidManual.StatusCode);
    }
}
