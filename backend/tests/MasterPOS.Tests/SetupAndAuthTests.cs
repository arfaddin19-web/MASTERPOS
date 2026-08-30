using System.Net;
using MasterPOS.Application.Auth;
using MasterPOS.Application.Setup;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class SetupAndAuthTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task Setup_status_reports_complete_once_the_fixture_has_run_it()
    {
        var response = await fixture.AdminClient.GetJsonAsync<SetupStatusResponse>("/api/setup/status");

        Assert.NotNull(response);
        Assert.True(response!.IsSetupComplete);
    }

    [Fact]
    public async Task Setup_rejects_a_second_call_once_a_company_exists()
    {
        var response = await fixture.AdminClient.PostJsonAsync("/api/setup", new SetupCompanyRequest(
            CompanyName: "Second Company",
            BusinessType: "Cafe",
            TaxRegistrationType: "Pan",
            VatRegistrationNumber: null,
            VatRatePercent: 13,
            PayrollEnabled: false,
            BranchName: "Branch",
            City: null,
            Address: null,
            Phone: null,
            AdminFullName: "Someone",
            AdminUsername: "someone",
            AdminPassword: "Someone@12345",
            AdminEmail: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_the_right_password_returns_a_token_and_the_full_permission_matrix()
    {
        var response = await fixture.Factory.CreateClient().PostJsonAsync(
            "/api/auth/login", new LoginRequest("test-admin", "TestAdmin@12345!"));

        var login = await response.ReadAsAsync<LoginResponse>();

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal("Admin", login.RoleName);
        // The seeded Admin role has every PermissionModule at full access —
        // the login response's own copy of that matrix should say so too,
        // since the client builds its UI from this without a second call.
        Assert.NotEmpty(login.Permissions);
        Assert.All(login.Permissions, p => Assert.True(p.CanView && p.CanCreate && p.CanEdit && p.CanDelete && p.CanApprove));
    }

    [Fact]
    public async Task Login_with_the_wrong_password_is_rejected()
    {
        var response = await fixture.Factory.CreateClient().PostJsonAsync(
            "/api/auth/login", new LoginRequest("test-admin", "definitely-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_to_an_authorized_endpoint_are_rejected()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/api/masters/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
