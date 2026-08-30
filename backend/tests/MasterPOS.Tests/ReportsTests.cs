using MasterPOS.Application.Inventory;
using MasterPOS.Application.Purchase;
using MasterPOS.Application.Reports;
using MasterPOS.Application.Sales;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

/// <summary>
/// All tests in <see cref="ApiCollection"/> share one database, so a report
/// endpoint that sums "everything in a date range" sees every other test's
/// data too. These tests assert the *delta* a known transaction produces —
/// snapshot before, do the thing, snapshot after — rather than an absolute
/// total, so they hold regardless of what else ran earlier in the suite or
/// what order the test runner picks.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReportsTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;
    private static string TodayRange => $"fromDate={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}&toDate={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}";

    [Fact]
    public async Task Sales_summary_reflects_exactly_one_new_Paid_order()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Report Sales Store");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Report Sales Item", defaultWarehouseId: warehouse.Id, salePrice: 200, isVatApplicable: true);
        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, product.Id, 10, 100, DateOnly.FromDateTime(DateTime.UtcNow)));

        var before = await Client.GetJsonAsync<SalesSummaryDto>($"/api/reports/sales-summary?{TodayRange}");

        var order = await (await Client.PostJsonAsync("/api/sales/orders", new CreateOrderRequest("Takeaway", null, null, null))).ReadAsAsync<OrderDto>();
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/lines", new AddOrderLineRequest(product.Id, 1, null));
        var current = await Client.GetJsonAsync<OrderDto>($"/api/sales/orders/{order.Id}");
        await Client.PostJsonAsync($"/api/sales/orders/{order.Id}/payments", new AddPaymentRequest(current!.GrandTotalAmount, "Cash", null));

        var after = await Client.GetJsonAsync<SalesSummaryDto>($"/api/reports/sales-summary?{TodayRange}");

        Assert.Equal(before!.OrderCount + 1, after!.OrderCount);
        Assert.Equal(before.GrandTotal + current.GrandTotalAmount, after.GrandTotal);
    }

    [Fact]
    public async Task Purchase_summary_reflects_exactly_one_newly_posted_invoice()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Report Purchase Store");
        var supplier = await MastersTestHelpers.CreatePartyAsync(Client, "Supplier", "Report Purchase Supplier");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Report Purchase Item", defaultWarehouseId: warehouse.Id);

        var before = await Client.GetJsonAsync<PurchaseSummaryDto>($"/api/reports/purchase-summary?{TodayRange}");

        var invoice = await (await Client.PostJsonAsync("/api/purchase/invoices", new CreatePurchaseInvoiceRequest(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null))).ReadAsAsync<PurchaseInvoiceDto>();
        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines", new AddPurchaseInvoiceLineRequest(product.Id, unit.Id, 10, 500, 0, 0)); // Rs.5000
        var posted = await (await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/post", new { })).ReadAsAsync<PurchaseInvoiceDto>();

        var after = await Client.GetJsonAsync<PurchaseSummaryDto>($"/api/reports/purchase-summary?{TodayRange}");

        Assert.Equal(before!.InvoiceCount + 1, after!.InvoiceCount);
        Assert.Equal(before.InvoiceTotal + posted.GrandTotalAmount, after.InvoiceTotal);
    }

    [Fact]
    public async Task Stock_valuation_increases_by_exactly_the_opening_stock_just_recorded()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Report Valuation Store");
        // The stock-valuation report values balances at the product master's
        // own PurchasePrice, not the per-entry unit cost passed to opening
        // stock — so the two must agree for a predictable delta here.
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Report Valuation Item", defaultWarehouseId: warehouse.Id, purchasePrice: 75);

        var before = await Client.GetJsonAsync<StockValuationDto>("/api/reports/stock-valuation");

        await Client.PostJsonAsync("/api/inventory/opening-stock", new CreateOpeningStockRequest(warehouse.Id, product.Id, 20, 75, DateOnly.FromDateTime(DateTime.UtcNow))); // Rs.1500

        var after = await Client.GetJsonAsync<StockValuationDto>("/api/reports/stock-valuation");

        Assert.Equal(before!.TotalValue + 1500, after!.TotalValue);
    }

    [Fact]
    public async Task Vat_summary_nets_sales_VAT_collected_against_purchase_VAT_paid()
    {
        var vatSummary = await Client.GetJsonAsync<VatSummaryDto>($"/api/reports/vat-summary?{TodayRange}");

        // Not a specific-value assertion (shared-database totals aren't
        // reliably reproducible), but the report's own arithmetic must hold:
        // whatever it reports as net payable is exactly collected minus paid.
        Assert.Equal(vatSummary!.SalesVatCollected - vatSummary.PurchaseVatPaid, vatSummary.NetVatPayable);
    }
}
