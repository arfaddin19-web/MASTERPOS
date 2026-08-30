using System.Net;
using MasterPOS.Application.Accounting;
using MasterPOS.Application.Purchase;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class AccountingTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    [Fact]
    public async Task Chart_of_accounts_seed_defaults_creates_protected_system_accounts()
    {
        // ApiTestFixture already seeded the defaults once during setup (it
        // has to happen there — seeding only succeeds while the chart of
        // accounts is completely empty, so it can't wait for whichever test
        // method happens to run first). Reseeding on top of that must fail.
        var reseed = await Client.PostJsonAsync("/api/accounting/chart-of-accounts/seed-defaults", new { });
        Assert.Equal(HttpStatusCode.BadRequest, reseed.StatusCode);

        var accounts = await Client.GetJsonAsync<List<ChartOfAccountDto>>("/api/accounting/chart-of-accounts");
        var cash = Assert.Single(accounts!, a => a.Name == "Cash");
        Assert.True(cash.IsSystemAccount);

        var deleteSystemAccount = await Client.DeleteAsync($"/api/accounting/chart-of-accounts/{cash.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteSystemAccount.StatusCode);
    }

    [Fact]
    public async Task Journal_entry_only_posts_once_debits_equal_credits()
    {
        var cash = await GetOrCreateAccountAsync("Cash", "Asset");
        var equity = await GetOrCreateAccountAsync("Opening Balance Equity", "Equity");

        var entry = await (await Client.PostJsonAsync("/api/accounting/journal-entries",
            new CreateJournalEntryRequest(DateOnly.FromDateTime(DateTime.UtcNow), "Test entry"))).ReadAsAsync<JournalEntryDto>();

        var postEmpty = await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, postEmpty.StatusCode);

        await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/lines",
            new AddJournalEntryLineRequest(cash.Id, 5000, 0, null));

        var postSingleLine = await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, postSingleLine.StatusCode);

        // Unbalanced: credit is only 4000 against a 5000 debit.
        await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/lines",
            new AddJournalEntryLineRequest(equity.Id, 0, 4000, null));
        var postUnbalanced = await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, postUnbalanced.StatusCode);

        await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/lines",
            new AddJournalEntryLineRequest(equity.Id, 0, 1000, null));
        var postBalanced = await (await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/post", new { })).ReadAsAsync<JournalEntryDto>();
        Assert.Equal("Posted", postBalanced.Status);
        Assert.Equal(5000, postBalanced.TotalDebit);
        Assert.Equal(5000, postBalanced.TotalCredit);
    }

    [Fact]
    public async Task Journal_line_must_be_one_sided_not_both_debit_and_credit()
    {
        var cash = await GetOrCreateAccountAsync("Cash", "Asset");
        var entry = await (await Client.PostJsonAsync("/api/accounting/journal-entries",
            new CreateJournalEntryRequest(DateOnly.FromDateTime(DateTime.UtcNow), null))).ReadAsAsync<JournalEntryDto>();

        var bothSides = await Client.PostJsonAsync($"/api/accounting/journal-entries/{entry.Id}/lines",
            new AddJournalEntryLineRequest(cash.Id, 100, 100, null));

        Assert.Equal(HttpStatusCode.BadRequest, bothSides.StatusCode);
    }

    [Fact]
    public async Task Party_payment_against_a_purchase_invoice_advances_its_own_AmountPaid()
    {
        var unit = await MastersTestHelpers.CreateUnitAsync(Client);
        var warehouse = await MastersTestHelpers.CreateWarehouseAsync(Client, fixture.BranchId, "Payment Link Store");
        var supplier = await MastersTestHelpers.CreatePartyAsync(Client, "Supplier", "Payment Link Supplier");
        var product = await MastersTestHelpers.CreateProductAsync(Client, unit.Id, "Payment Link Item", defaultWarehouseId: warehouse.Id);
        var invoice = await (await Client.PostJsonAsync("/api/purchase/invoices", new CreatePurchaseInvoiceRequest(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null))).ReadAsAsync<PurchaseInvoiceDto>();
        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/lines", new AddPurchaseInvoiceLineRequest(product.Id, unit.Id, 10, 1000, 0, 0)); // Rs.10,000

        // A party payment referencing a purchase invoice requires the
        // invoice to be Posted first — same rule PurchaseInvoiceService's
        // own payment endpoint enforces.
        await Client.PostJsonAsync($"/api/purchase/invoices/{invoice.Id}/post", new { });

        var overpay = await Client.PostJsonAsync("/api/accounting/party-payments", new CreatePartyPaymentRequest(
            supplier.Id, "Paid", 20000, "BankTransfer", "PurchaseInvoice", invoice.Id, DateOnly.FromDateTime(DateTime.UtcNow), null));
        Assert.Equal(HttpStatusCode.BadRequest, overpay.StatusCode);

        var payment = await Client.PostJsonAsync("/api/accounting/party-payments", new CreatePartyPaymentRequest(
            supplier.Id, "Paid", 6000, "BankTransfer", "PurchaseInvoice", invoice.Id, DateOnly.FromDateTime(DateTime.UtcNow), null));
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);

        var updatedInvoice = await Client.GetJsonAsync<PurchaseInvoiceDto>($"/api/purchase/invoices/{invoice.Id}");
        Assert.Equal(6000, updatedInvoice!.AmountPaid);
    }

    [Fact]
    public async Task Opening_balance_requires_exactly_one_of_party_or_account()
    {
        var neither = await Client.PostJsonAsync("/api/accounting/opening-balances",
            new UpsertOpeningBalanceRequest(null, null, 1000, "Dr", DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.BadRequest, neither.StatusCode);

        var party = await MastersTestHelpers.CreatePartyAsync(Client, "Customer", "Opening Balance Party");
        var account = await GetOrCreateAccountAsync("Bank", "Asset");
        var both = await Client.PostJsonAsync("/api/accounting/opening-balances",
            new UpsertOpeningBalanceRequest(party.Id, account.Id, 1000, "Dr", DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);

        var justParty = await Client.PostJsonAsync("/api/accounting/opening-balances",
            new UpsertOpeningBalanceRequest(party.Id, null, 1000, "Dr", DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.OK, justParty.StatusCode);
    }

    private async Task<ChartOfAccountDto> GetOrCreateAccountAsync(string name, string accountType)
    {
        var accounts = await Client.GetJsonAsync<List<ChartOfAccountDto>>("/api/accounting/chart-of-accounts");
        var existing = accounts!.FirstOrDefault(a => a.Name == name);
        if (existing is not null) return existing;

        var created = await Client.PostJsonAsync("/api/accounting/chart-of-accounts", new UpsertChartOfAccountRequest(name, accountType, null));
        return await created.ReadAsAsync<ChartOfAccountDto>();
    }
}
