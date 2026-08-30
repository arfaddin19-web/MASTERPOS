using System.Net;
using MasterPOS.Application.Inventory;
using MasterPOS.Application.Masters;
using MasterPOS.Application.Sales;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class SalesOrderTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    private async Task<(ProductDto Rice, ProductDto Dal, ProductDto VegThali, ProductDto Delivery, ProductDto ThermalRoll, WarehouseDto Warehouse)> SeedMenuAsync()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client, "Kg");
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Sales Test Store");

        var rice = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Rice", productType: "Inventory", defaultWarehouseId: warehouse.Id, salePrice: 100, isVatApplicable: true);
        var dal = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Dal", productType: "Inventory", defaultWarehouseId: warehouse.Id, salePrice: 80, isVatApplicable: true);
        var vegThali = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Veg Thali", productType: "Recipe", salePrice: 280, isVatApplicable: true);
        var delivery = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Delivery Charge", productType: "Service", salePrice: 100, isVatApplicable: false);
        var thermalRoll = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Thermal Roll", productType: "Consumable", defaultWarehouseId: warehouse.Id, salePrice: 20);

        await Client.PutJsonAsync($"/api/masters/products/{vegThali.Id}/bom", new SetProductBomRequest([
            new SetProductBomLine(rice.Id, 0.2m),
            new SetProductBomLine(dal.Id, 0.15m),
        ]));

        // Give Rice/Dal enough opening stock that closing the order below never
        // goes negative — the exact quantities feed the stock-deduction assertions.
        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, rice.Id, 50, 60, DateOnly.FromDateTime(DateTime.UtcNow)));
        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, dal.Id, 50, 60, DateOnly.FromDateTime(DateTime.UtcNow)));

        return (rice, dal, vegThali, delivery, thermalRoll, warehouse);
    }

    [Fact]
    public async Task Consumable_products_cannot_be_added_to_an_order()
    {
        var (_, _, _, _, thermalRoll, _) = await SeedMenuAsync();
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();

        var response = await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(thermalRoll.Id, 1, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Order_total_is_computed_correctly_across_a_VAT_line_a_Recipe_line_and_a_VAT_exempt_Service_line()
    {
        var (_, _, vegThali, delivery, _, _) = await SeedMenuAsync();
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();

        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(vegThali.Id, 2, null));
        var afterDelivery = await (await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(delivery.Id, 1, null))).ReadAsAsync<OrderDto>();

        // 2 x Rs.280 Veg Thali (VAT-applicable) + Rs.100 Delivery (VAT-exempt).
        Assert.Equal(660m, afterDelivery.SubTotalAmount);
        Assert.Equal(72.80m, afterDelivery.VatAmount); // 13% of the Rs.560 vatable portion only.
        Assert.Equal(733m, afterDelivery.GrandTotalAmount); // 660 + 72.80, rounded to the nearest 0.05/whole rupee.
    }

    [Fact]
    public async Task Manual_percent_discount_is_prorated_before_VAT_not_after()
    {
        var (_, _, vegThali, delivery, _, _) = await SeedMenuAsync();
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(vegThali.Id, 1, null)); // Rs.280, VAT-applicable
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(delivery.Id, 1, null)); // Rs.100, VAT-exempt

        var discounted = await (await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/discount/manual",
            new ApplyManualDiscountRequest("Percent", 10))).ReadAsAsync<OrderDto>();

        // Subtotal Rs.380, 10% off = Rs.38 discount, prorated 280/380 to the
        // vatable portion: Rs.28 off the Rs.280 vatable base, leaving Rs.252
        // taxable at 13% = Rs.32.76 VAT — not 13% of the full Rs.280.
        Assert.Equal(380m, discounted.SubTotalAmount);
        Assert.Equal(38m, discounted.DiscountAmount);
        Assert.Equal(32.76m, discounted.VatAmount);
    }

    [Fact]
    public async Task Closing_an_order_deducts_Recipe_BOM_components_not_the_Recipe_itself_and_never_the_Service_line()
    {
        var (rice, dal, vegThali, delivery, _, warehouse) = await SeedMenuAsync();
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(vegThali.Id, 2, null));
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(delivery.Id, 1, null));
        var current = await Client.GetJsonAsync<OrderDto>($"/api/sales/orders/{order.Id}");

        var paid = await (await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/payments",
            new AddPaymentRequest(current!.GrandTotalAmount, "Cash", null))).ReadAsAsync<OrderDto>();
        Assert.Equal("Paid", paid.Status);

        var riceLedger = await Client.GetJsonAsync<List<StockLedgerEntryDto>>($"/api/inventory/reports/ledger?productId={rice.Id}&warehouseId={warehouse.Id}");
        var dalLedger = await Client.GetJsonAsync<List<StockLedgerEntryDto>>($"/api/inventory/reports/ledger?productId={dal.Id}&warehouseId={warehouse.Id}");

        var riceOut = riceLedger!.Where(e => e.ReferenceType == "Order").Sum(e => e.QuantityOut);
        var dalOut = dalLedger!.Where(e => e.ReferenceType == "Order").Sum(e => e.QuantityOut);
        // 2x Veg Thali x (0.2kg Rice + 0.15kg Dal per plate) = 0.4kg Rice, 0.3kg Dal out.
        Assert.Equal(0.4m, riceOut);
        Assert.Equal(0.3m, dalOut);

        // The Recipe product itself and the Service line never carry stock —
        // there must be no Order-referenced ledger row for either.
        var vegThaliLedger = await Client.GetJsonAsync<List<StockLedgerEntryDto>>($"/api/inventory/reports/ledger?productId={vegThali.Id}");
        Assert.DoesNotContain(vegThaliLedger!, e => e.ReferenceType == "Order");
    }

    [Fact]
    public async Task A_closed_order_rejects_further_line_changes()
    {
        var (_, _, _, delivery, _, _) = await SeedMenuAsync();
        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(delivery.Id, 1, null));
        var current = await Client.GetJsonAsync<OrderDto>($"/api/sales/orders/{order.Id}");
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/payments", new AddPaymentRequest(current!.GrandTotalAmount, "Cash", null));

        var response = await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(delivery.Id, 1, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task KOT_sends_only_pending_station_lines_and_blocks_an_empty_reprint()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client, "Plate");
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "KOT Test Store");
        var kitchenDish = await Client.PostJsonAsync("/api/masters/products", new UpsertProductRequest(
            ApiTestFixture.Unique("Kitchen Dish"), "Inventory", null, null, unit.Id, warehouse.Id, null,
            50, 150, true, 0, "Kitchen", null, true, true));
        var dish = await kitchenDish.ReadAsAsync<ProductDto>();
        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, dish.Id, 20, 50, DateOnly.FromDateTime(DateTime.UtcNow)));

        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(dish.Id, 1, null));

        var firstPrint = await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/kot", new { });
        var firstResult = await firstPrint.ReadAsAsync<List<KotPrintResultDto>>();
        var kitchenTicket = Assert.Single(firstResult);
        Assert.Equal("Kitchen", kitchenTicket.Station);
        Assert.Equal(1, kitchenTicket.LineCount);
        Assert.False(kitchenTicket.IsReprint);

        // Nothing became newly Pending since that first print — reprinting
        // immediately with no new lines is rejected, not silently a no-op.
        var reprint = await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/kot", new { });
        Assert.Equal(HttpStatusCode.BadRequest, reprint.StatusCode);
    }
}
