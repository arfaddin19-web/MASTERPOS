using System.Net.Http.Headers;
using MasterPOS.Application.Auth;
using MasterPOS.Application.Setup;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MasterPOS.Tests.Testing;

/// <summary>
/// One real ASP.NET Core host (via <see cref="WebApplicationFactory{TEntryPoint}"/>)
/// backed by one real SQL Server database, shared across every test class in
/// the assembly via <see cref="ApiCollection"/> — the same "against a real,
/// running system" standard the whole backend was hand-verified against
/// during development, just automated now.
///
/// Setup is one-shot per company (the backend itself enforces that — a
/// second <c>POST /api/setup</c> is rejected), so this fixture runs it
/// exactly once and hands every test the same admin session. Tests that
/// need isolation from each other create their own uniquely-named records
/// (a GUID suffix on names) rather than each getting a private database —
/// that mirrors real usage anyway: one company, many concurrent documents.
/// </summary>
public class ApiTestFixture : IAsyncLifetime
{
    private const string AdminUsername = "test-admin";
    private const string AdminPassword = "TestAdmin@12345!";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient AdminClient { get; private set; } = null!;
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public Guid AdminRoleId { get; private set; }

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("MASTERPOS_TEST_CONNECTION_STRING")
        ?? "Server=localhost,14330;Database=MasterPOSTests;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        // The same override mechanism the backend's own README documents for
        // a real install: the standard ASP.NET Core ConnectionStrings__Default
        // env var (double underscore) beats appsettings.json's placeholder
        // because environment variables are always the last configuration
        // source Program.cs's WebApplication.CreateBuilder adds — unlike
        // WebApplicationFactory's ConfigureAppConfiguration hook, which (for
        // a minimal-API Program.cs) runs too early to reliably out-rank
        // appsettings.json's own already-loaded value.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);

        Factory = new WebApplicationFactory<Program>();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterPosDbContext>();
            await db.Database.MigrateAsync();
        }

        var probeClient = Factory.CreateClient();
        var status = await probeClient.GetJsonAsync<SetupStatusResponse>("/api/setup/status");

        if (status is { IsSetupComplete: false })
        {
            var setupResponse = await probeClient.PostJsonAsync("/api/setup", new SetupCompanyRequest(
                CompanyName: "Automated Test Traders",
                BusinessType: "Trading",
                TaxRegistrationType: "Vat",
                VatRegistrationNumber: "600000000",
                VatRatePercent: 13,
                PayrollEnabled: true,
                BranchName: "Main Branch",
                City: "Kathmandu",
                Address: null,
                Phone: null,
                AdminFullName: "Test Admin",
                AdminUsername: AdminUsername,
                AdminPassword: AdminPassword,
                AdminEmail: null));
            var setup = await setupResponse.ReadAsAsync<SetupCompanyResponse>();
            CompanyId = setup.CompanyId;
            BranchId = setup.BranchId;
            AdminUserId = setup.AdminUserId;
            AdminRoleId = setup.AdminRoleId;
        }

        var loginResponse = await probeClient.PostJsonAsync(
            "/api/auth/login", new LoginRequest(AdminUsername, AdminPassword));
        var login = await loginResponse.ReadAsAsync<LoginResponse>();
        CompanyId = login.CompanyId;
        BranchId = login.DefaultBranchId ?? throw new InvalidOperationException("Setup did not assign a default branch to the admin.");
        AdminUserId = login.UserId;

        AdminClient = Factory.CreateClient();
        AdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Seed the protected system chart-of-accounts exactly once here so
        // every AccountingTests method sees the same "Cash"/"Bank"/etc. —
        // seeding only succeeds while the chart of accounts is completely
        // empty, so this can't be left to whichever test happens to run
        // first in the shared collection.
        await AdminClient.PostJsonAsync("/api/accounting/chart-of-accounts/seed-defaults", new { });
    }

    public Task DisposeAsync()
    {
        AdminClient.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>A short, collision-proof suffix for test data names — lets
    /// every test create its own "Rice-ab12cd34" instead of colliding with
    /// the same-named row another test (or another run against a reused
    /// database) already left behind.</summary>
    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiTestFixture>
{
    public const string Name = "Api";
}
