using System.Net;
using MasterPOS.Application.Auth;
using MasterPOS.Domain.Common;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class RolesAndUsersTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    private static List<RolePermissionInput> FullModuleMatrix(bool value = true) =>
        Enum.GetValues<PermissionModule>().Select(m => new RolePermissionInput(m.ToString(), value, value, value, value, value)).ToList();

    [Fact]
    public async Task Role_permissions_must_cover_every_module_exactly_once()
    {
        var missingOne = FullModuleMatrix();
        missingOne.RemoveAt(0);
        var missingResponse = await Client.PostJsonAsync("/api/auth/roles", new UpsertRoleRequest(ApiTestFixture.Unique("Incomplete Role"), missingOne));
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);

        var duplicated = FullModuleMatrix();
        duplicated.Add(duplicated[0]);
        var duplicateResponse = await Client.PostJsonAsync("/api/auth/roles", new UpsertRoleRequest(ApiTestFixture.Unique("Duplicated Role"), duplicated));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task System_Admin_role_cannot_be_edited_or_deleted()
    {
        var roles = await Client.GetJsonAsync<List<RoleDto>>("/api/auth/roles");
        var admin = Assert.Single(roles!, r => r.IsSystemRole);

        var edit = await Client.PutJsonAsync($"/api/auth/roles/{admin.Id}", new UpsertRoleRequest("Renamed Admin", FullModuleMatrix()));
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);

        var delete = await Client.DeleteAsync($"/api/auth/roles/{admin.Id}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in_even_with_the_right_password_and_cannot_deactivate_themselves()
    {
        var role = await (await Client.PostJsonAsync("/api/auth/roles",
            new UpsertRoleRequest(ApiTestFixture.Unique("Cashier"), FullModuleMatrix(false)))).ReadAsAsync<RoleDto>();

        var username = ApiTestFixture.Unique("cashier").ToLowerInvariant();
        const string password = "Cashier@12345!";
        var user = await (await Client.PostJsonAsync("/api/auth/users",
            new CreateUserRequest("Test Cashier", null, username, password, role.Id, fixture.BranchId, null))).ReadAsAsync<UserDto>();

        var loginBeforeDeactivate = await fixture.Factory.CreateClient().PostJsonAsync("/api/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, loginBeforeDeactivate.StatusCode);

        // The admin account (this test's own client) can't deactivate itself...
        var selfDeactivate = await Client.PatchJsonAsync($"/api/auth/users/{fixture.AdminUserId}/active", new SetUserActiveRequest(false));
        Assert.Equal(HttpStatusCode.BadRequest, selfDeactivate.StatusCode);

        // ...but can deactivate someone else, after which their login is rejected.
        var deactivate = await Client.PatchJsonAsync($"/api/auth/users/{user.Id}/active", new SetUserActiveRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var loginAfterDeactivate = await fixture.Factory.CreateClient().PostJsonAsync("/api/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfterDeactivate.StatusCode);
    }

    [Fact]
    public async Task Reset_password_invalidates_the_old_password_and_accepts_the_new_one()
    {
        var role = await (await Client.PostJsonAsync("/api/auth/roles",
            new UpsertRoleRequest(ApiTestFixture.Unique("ResetTest"), FullModuleMatrix(false)))).ReadAsAsync<RoleDto>();
        var username = ApiTestFixture.Unique("resettest").ToLowerInvariant();
        var user = await (await Client.PostJsonAsync("/api/auth/users",
            new CreateUserRequest("Reset Test", null, username, "OldPassword@123", role.Id, fixture.BranchId, null))).ReadAsAsync<UserDto>();

        await Client.PostJsonAsync($"/api/auth/users/{user.Id}/reset-password", new ResetPasswordRequest("NewPassword@456"));

        var oldPasswordLogin = await fixture.Factory.CreateClient().PostJsonAsync("/api/auth/login", new LoginRequest(username, "OldPassword@123"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await fixture.Factory.CreateClient().PostJsonAsync("/api/auth/login", new LoginRequest(username, "NewPassword@456"));
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }
}
