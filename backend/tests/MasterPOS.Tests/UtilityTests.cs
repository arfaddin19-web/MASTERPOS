using System.Net;
using MasterPOS.Application.Auth;
using MasterPOS.Application.Utility;
using MasterPOS.Domain.Common;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class UtilityTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    [Fact]
    public async Task Kot_printer_accepts_a_station_and_a_receipt_printer_rejects_one()
    {
        var kot = await Client.PostJsonAsync("/api/utility/printers", new UpsertPrinterRequest(
            fixture.BranchId, ApiTestFixture.Unique("Kitchen Printer"), "Kot", "Kitchen", null, true));
        Assert.Equal(HttpStatusCode.OK, kot.StatusCode);

        var receiptWithStation = await Client.PostJsonAsync("/api/utility/printers", new UpsertPrinterRequest(
            fixture.BranchId, ApiTestFixture.Unique("Counter Printer"), "Receipt", "Kitchen", null, true));
        Assert.Equal(HttpStatusCode.BadRequest, receiptWithStation.StatusCode);
    }

    [Fact]
    public async Task Payment_modes_lazy_seed_all_five_with_Cash_and_Card_on_by_default()
    {
        var modes = await Client.GetJsonAsync<List<PaymentModeSettingDto>>("/api/utility/payment-modes");

        Assert.Equal(5, modes!.Count);
        Assert.Equal(
            new[] { "Cash", "Card", "ESewa", "Khalti", "BankTransfer" }.OrderBy(c => c),
            modes.Select(m => m.Code).OrderBy(c => c));
        Assert.True(modes.Single(m => m.Code == "Cash").IsEnabled);
        Assert.True(modes.Single(m => m.Code == "Card").IsEnabled);
    }

    [Fact]
    public async Task Toggling_a_payment_mode_persists()
    {
        await Client.GetJsonAsync<List<PaymentModeSettingDto>>("/api/utility/payment-modes"); // ensure seeded

        var disabled = await (await Client.PatchJsonAsync("/api/utility/payment-modes/ESewa", new SetPaymentModeEnabledRequest(false)))
            .ReadAsAsync<PaymentModeSettingDto>();
        Assert.False(disabled.IsEnabled);

        var reEnabled = await (await Client.PatchJsonAsync("/api/utility/payment-modes/ESewa", new SetPaymentModeEnabledRequest(true)))
            .ReadAsAsync<PaymentModeSettingDto>();
        Assert.True(reEnabled.IsEnabled);
    }

    [Fact]
    public async Task Audit_log_records_a_role_creation()
    {
        // RoleService logs under the "Auth.Roles" entity type — not the bare
        // module name.
        var before = await Client.GetJsonAsync<List<AuditLogEntryDto>>("/api/utility/audit-log?entityType=Auth.Roles");

        var roleName = ApiTestFixture.Unique("Audited Role");
        var permissions = Enum.GetValues<PermissionModule>()
            .Select(m => new RolePermissionInput(m.ToString(), true, false, false, false, false))
            .ToList();
        await Client.PostJsonAsync("/api/auth/roles", new UpsertRoleRequest(roleName, permissions));

        var after = await Client.GetJsonAsync<List<AuditLogEntryDto>>("/api/utility/audit-log?entityType=Auth.Roles");

        Assert.True(after!.Count > before!.Count);
        Assert.Contains(after, e => e.Description.Contains(roleName));
    }

    [Fact]
    public async Task An_unconfigured_backup_directory_fails_with_a_clear_message_not_a_stack_trace()
    {
        // This test host never sets Backup:Directory — matching a fresh
        // install before the on-site admin configures it.
        var response = await Client.PostJsonAsync("/api/utility/backups", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("directory", body, StringComparison.OrdinalIgnoreCase);
    }
}
