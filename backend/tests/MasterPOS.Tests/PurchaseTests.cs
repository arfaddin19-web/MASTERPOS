using System.Net;
using MasterPOS.Application.Inventory;
using MasterPOS.Application.Purchase;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class PurchaseTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    [Fact]
    public async Task Purchase_invoice_line_math_matches_the_original_design_worked_example()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client, "Bag");
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Purchase Math Store");
        var supplier = await MastersTestHelpers.CreatePartyAsync(Client, "Supplier", "Line Math Supplier");
        var rice = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Purchase Rice", productType: "Inventory", defaultWarehouseId: warehouse.Id);

        var invoice = await (await Client.PostJsonAsync("/api/purchase/invoices", new CreatePurchaseInvoiceRequest(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null))).ReadAsAsync<PurchaseInvoiceDto>();

        // 100 x Rs.350 = 35,000 subtotal; 5% discount = 1,750 off, leaving
        // 33,250 taxable; +13% VAT (4,322.50) = Rs.37,572.50.
        var withLine = await (await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines",
            new AddPurchaseInvoiceLineRequest(rice.Id, unit.Id, 100, 350, 5, 13))).ReadAsAsync<PurchaseInvoiceDto>();

        var line = Assert.Single(withLine.Lines);
        Assert.Equal(37572.50m, line.LineAmount);
    }

    [Fact]
    public async Task Posting_an_invoice_moves_stock_and_freezes_it_for_further_edits()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Post Test Store");
        var supplier = await MastersTestHelpers.CreatePartyAsync(Client, "Supplier", "Post Test Supplier");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Postable Item", defaultWarehouseId: warehouse.Id);

        var invoice = await (await Client.PostJsonAsync("/api/purchase/invoices", new CreatePurchaseInvoiceRequest(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null))).ReadAsAsync<PurchaseInvoiceDto>();
        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines", new AddPurchaseInvoiceLineRequest(product.Id, unit.Id, 20, 100, 0, 0));

        var posted = await (await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/post", new { })).ReadAsAsync<PurchaseInvoiceDto>();
        Assert.Equal("Posted", posted.Status);

        var ledger = await Client.GetJsonAsync<List<StockLedgerEntryDto>>($"/api/inventory/reports/ledger?productId={product.Id}&warehouseId={warehouse.Id}");
        var purchaseRow = Assert.Single(ledger!, e => e.ReferenceType == "PurchaseInvoice");
        Assert.Equal(20, purchaseRow.QuantityIn);

        // Frozen: no more line edits, no re-cancel.
        var lineEditAfterPost = await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines",
            new AddPurchaseInvoiceLineRequest(product.Id, unit.Id, 1, 100, 0, 0));
        Assert.Equal(HttpStatusCode.BadRequest, lineEditAfterPost.StatusCode);

        var cancelAfterPost = await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/cancel", new { });
        Assert.Equal(HttpStatusCode.BadRequest, cancelAfterPost.StatusCode);
    }

    [Fact]
    public async Task Payment_requires_the_invoice_to_be_Posted_then_advances_AmountPaid()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Payment Test Store");
        var supplier = await MastersTestHelpers.CreatePartyAsync(Client, "Supplier", "Payment Test Supplier");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Payment Test Item", defaultWarehouseId: warehouse.Id);

        var invoice = await (await Client.PostJsonAsync("/api/purchase/invoices", new CreatePurchaseInvoiceRequest(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null))).ReadAsAsync<PurchaseInvoiceDto>();
        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines", new AddPurchaseInvoiceLineRequest(product.Id, unit.Id, 10, 100, 0, 0)); // Rs.1000

        // A Draft invoice can't take a payment yet.
        var tooEarly = await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/payments", new RecordPurchasePaymentRequest(400));
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);

        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/post", new { });

        var paid = await (await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/payments",
            new RecordPurchasePaymentRequest(400))).ReadAsAsync<PurchaseInvoiceDto>();

        Assert.Equal(400, paid.AmountPaid);
        Assert.Equal(600, paid.AmountRemaining);
    }

    [Fact]
    public async Task Purchase_return_against_a_posted_invoice_reconciles_net_stock()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client, "Kg");
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Return Test Store");
        var supplier = await MastersTestHelpers.CreatePartyAsync(Client, "Supplier", "Return Test Supplier");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Returnable Item", defaultWarehouseId: warehouse.Id);

        var invoice = await (await Client.PostJsonAsync("/api/purchase/invoices", new CreatePurchaseInvoiceRequest(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null))).ReadAsAsync<PurchaseInvoiceDto>();
        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines", new AddPurchaseInvoiceLineRequest(product.Id, unit.Id, 100, 350, 0, 0));
        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/post", new { });

        var ret = await (await Client.PostJsonAsync("/api/purchase/returns", new CreatePurchaseReturnRequest(
            supplier.Id, invoice.Id, DateOnly.FromDateTime(DateTime.UtcNow), null))).ReadAsAsync<PurchaseReturnDto>();
        await Client.PostJsonAsync($"/api/purchase/returns/{ret.Id}/lines", new AddPurchaseReturnLineRequest(product.Id, unit.Id, 5, 350, 0));
        var postedReturn = await (await Client.PostJsonAsync($"/api/purchase/returns/{ret.Id}/post", new { })).ReadAsAsync<PurchaseReturnDto>();
        Assert.Equal("Posted", postedReturn.Status);

        var balances = await Client.GetJsonAsync<List<StockBalanceDto>>($"/api/inventory/reports/balances?warehouseId={warehouse.Id}");
        var balance = Assert.Single(balances!, b => b.ProductId == product.Id);
        Assert.Equal(95, balance.Balance); // 100 in, 5 out.
    }

    [Fact]
    public async Task Non_purchasable_product_types_are_rejected_on_a_purchase_line()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var supplier = await MastersTestHelpers.CreatePartyAsync(Client, "Supplier", "Reject Test Supplier");
        var service = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Non Purchasable Service", productType: "Service");

        var invoice = await (await Client.PostJsonAsync("/api/purchase/invoices", new CreatePurchaseInvoiceRequest(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null))).ReadAsAsync<PurchaseInvoiceDto>();

        var response = await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines",
            new AddPurchaseInvoiceLineRequest(service.Id, unit.Id, 1, 100, 0, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
