using System.Net;
using MasterPOS.Application.Workforce;
using MasterPOS.Tests.Testing;

namespace MasterPOS.Tests;

[Collection(ApiCollection.Name)]
public class WorkforceTests(ApiTestFixture fixture)
{
    private HttpClient Client => fixture.AdminClient;

    private async Task<EmployeeDto> CreateEmployeeAsync(decimal basicSalary = 20000, string maritalStatus = "Single")
    {
        var response = await Client.PostJsonAsync("/api/workforce/employees", new CreateEmployeeRequest(
            fixture.BranchId, ApiTestFixture.Unique("Employee"), "Tester", null,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)), basicSalary, null, null, maritalStatus));
        return await response.ReadAsAsync<EmployeeDto>();
    }

    [Fact]
    public async Task Employee_with_no_history_can_be_deleted_but_not_once_it_has_advance_history()
    {
        var employee = await CreateEmployeeAsync();
        var deletable = await Client.DeleteAsync($"/api/workforce/employees/{employee.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deletable.StatusCode);

        var locked = await CreateEmployeeAsync();
        await Client.PostJsonAsync("/api/workforce/advances", new CreateEmployeeAdvanceRequest(locked.Id, 1000, DateOnly.FromDateTime(DateTime.UtcNow), null));

        var deleteAfterHistory = await Client.DeleteAsync($"/api/workforce/employees/{locked.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteAfterHistory.StatusCode);

        var deactivate = await Client.PatchJsonAsync($"/api/workforce/employees/{locked.Id}/active", new SetEmployeeActiveRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
    }

    [Fact]
    public async Task Checking_in_twice_the_same_day_is_rejected()
    {
        var employee = await CreateEmployeeAsync();

        var first = await Client.PostJsonAsync("/api/workforce/attendance/check-in", new CheckInRequest(employee.Id));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Client.PostJsonAsync("/api/workforce/attendance/check-in", new CheckInRequest(employee.Id));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Leave_request_can_only_be_cancelled_while_still_Pending()
    {
        var employee = await CreateEmployeeAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var leave = await (await Client.PostJsonAsync("/api/workforce/leave-requests",
            new CreateLeaveRequestRequest(employee.Id, "Sick", today, today, null))).ReadAsAsync<LeaveRequestDto>();
        Assert.Equal("Pending", leave.Status);

        var approved = await (await Client.PostJsonAsync($"/api/workforce/leave-requests/{leave.Id}/approve", new { })).ReadAsAsync<LeaveRequestDto>();
        Assert.Equal("Approved", approved.Status);

        // Already decided — cancelling now is rejected, it's a historical record.
        var cancelAfterApprove = await Client.PostJsonAsync($"/api/workforce/leave-requests/{leave.Id}/cancel", new { });
        Assert.Equal(HttpStatusCode.BadRequest, cancelAfterApprove.StatusCode);
    }

    [Fact]
    public async Task Advance_recovery_is_capped_at_the_outstanding_balance()
    {
        var employee = await CreateEmployeeAsync();
        var advance = await (await Client.PostJsonAsync("/api/workforce/advances",
            new CreateEmployeeAdvanceRequest(employee.Id, 1000, DateOnly.FromDateTime(DateTime.UtcNow), null))).ReadAsAsync<EmployeeAdvanceDto>();

        var overRecover = await Client.PostJsonAsync($"/api/workforce/advances/{advance.Id}/recover", new RecordAdvanceRecoveryRequest(1500));
        Assert.Equal(HttpStatusCode.BadRequest, overRecover.StatusCode);

        var partial = await (await Client.PostJsonAsync($"/api/workforce/advances/{advance.Id}/recover",
            new RecordAdvanceRecoveryRequest(400))).ReadAsAsync<EmployeeAdvanceDto>();
        Assert.Equal(600, partial.Balance);
        Assert.Equal("PartiallyRecovered", partial.Status);

        var full = await (await Client.PostJsonAsync($"/api/workforce/advances/{advance.Id}/recover",
            new RecordAdvanceRecoveryRequest(600))).ReadAsAsync<EmployeeAdvanceDto>();
        Assert.Equal(0, full.Balance);
        Assert.Equal("Recovered", full.Status);
    }

    [Fact]
    public async Task Tax_slabs_seed_defaults_once_and_reject_an_overlapping_manual_slab()
    {
        // This company was seeded once already by another test in this
        // collection (they all share one company) — assert idempotently:
        // either this call seeds it now, or it's already seeded and a repeat
        // is rejected, but the end state (10 rows) holds either way.
        var seed = await Client.PostJsonAsync("/api/workforce/tax-slabs/seed-defaults", new { });
        if (seed.StatusCode == HttpStatusCode.OK)
        {
            var reseed = await Client.PostJsonAsync("/api/workforce/tax-slabs/seed-defaults", new { });
            Assert.Equal(HttpStatusCode.BadRequest, reseed.StatusCode);
        }

        var slabs = await Client.GetJsonAsync<List<TaxSlabDto>>("/api/workforce/tax-slabs");
        Assert.Equal(10, slabs!.Count);

        var overlap = await Client.PostJsonAsync("/api/workforce/tax-slabs", new UpsertTaxSlabRequest("Single", 100000, 300000, 5));
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
    }

    [Fact]
    public async Task Payroll_run_computes_PF_SSF_and_TDS_to_the_rupee_matching_hand_calculated_figures()
    {
        // A fixed, already-elapsed period (not "this month") keeps DaysInMonth
        // — and therefore the daily rate and every figure derived from it —
        // deterministic regardless of when this test actually runs.
        const byte periodMonth = 3; // March: 31 days.
        const short periodYear = 2023;

        await Client.PostJsonAsync("/api/workforce/tax-slabs/seed-defaults", new { }); // no-op (400) if already seeded — fine either way
        await Client.PutJsonAsync("/api/workforce/payroll-settings", new UpdatePayrollSettingsRequest(
            OvertimeEnabled: false, OvertimeMultiplier: 1.5m,
            PfEnabled: true, PfEmployeePercent: 10, PfEmployerPercent: 10,
            SsfEnabled: true, SsfEmployeePercent: 11, SsfEmployerPercent: 20,
            TdsEnabled: true,
            FestivalBonusEnabled: true, FestivalBonusPercent: 100));

        // Rs.31,000 / 31 days = a clean Rs.1,000/day, so "zero attendance
        // marked" (absentCount = 0) leaves BasicAmount at the full salary —
        // no month-long attendance marking needed to get an exact figure.
        var employee = await CreateEmployeeAsync(basicSalary: 31000, maritalStatus: "Single");

        var run = await (await Client.PostJsonAsync("/api/workforce/payroll-runs",
            new CreatePayrollRunRequest(fixture.BranchId, periodMonth, periodYear, "Monthly"))).ReadAsAsync<PayrollRunDto>();

        var line = Assert.Single(run.Lines, l => l.EmployeeId == employee.Id);
        Assert.Equal(31000m, line.BasicAmount);
        Assert.Equal(3100m, line.PfEmployeeAmount);
        Assert.Equal(3100m, line.PfEmployerAmount);
        Assert.Equal(3410m, line.SsfEmployeeAmount);
        Assert.Equal(6200m, line.SsfEmployerAmount);
        // Monthly taxable = 31000 - 3100 - 3410 = 24490; x12 = 293,880 —
        // entirely inside the first 1% Single band (Rs.0 - Rs.500,000).
        Assert.Equal(244.90m, line.TdsAmount);
        Assert.Equal(24245.10m, line.NetPayAmount); // 31000 - 3100 - 3410 - 244.90

        // A second Monthly run for the same branch/period is rejected...
        var duplicate = await Client.PostJsonAsync("/api/workforce/payroll-runs",
            new CreatePayrollRunRequest(fixture.BranchId, periodMonth, periodYear, "Monthly"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        // ...but a FestivalBonus run for the same period is a different
        // document entirely and coexists — 100% of basic salary, no OT/PF/SSF/TDS.
        var bonusRun = await (await Client.PostJsonAsync("/api/workforce/payroll-runs",
            new CreatePayrollRunRequest(fixture.BranchId, periodMonth, periodYear, "FestivalBonus"))).ReadAsAsync<PayrollRunDto>();
        var bonusLine = Assert.Single(bonusRun.Lines, l => l.EmployeeId == employee.Id);
        Assert.Equal(31000m, bonusLine.AllowancesAmount);
        Assert.Equal(0m, bonusLine.PfEmployeeAmount);
        Assert.Equal(0m, bonusLine.TdsAmount);

        // Completing the Monthly run locks it — recompute/complete again both fail.
        await Client.PostJsonAsync($"/api/workforce/payroll-runs/{run.Id}/complete", new { });
        var recomputeAfterComplete = await Client.PostJsonAsync($"/api/workforce/payroll-runs/{run.Id}/recompute", new { });
        Assert.Equal(HttpStatusCode.BadRequest, recomputeAfterComplete.StatusCode);
    }
}
