using MasterPOS.Application.Common;
using MasterPOS.Application.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workforce/payroll-runs")]
public class PayrollRunsController : ControllerBase
{
    private readonly IPayrollRunService _payrollRuns;

    public PayrollRunsController(IPayrollRunService payrollRuns) => _payrollRuns = payrollRuns;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayrollRunDto>>> List([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _payrollRuns.ListAsync(branchId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PayrollRunDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _payrollRuns.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>The "Run Payroll" button — creates the Draft run and computes
    /// every active employee's line in this one call.</summary>
    [HttpPost]
    public async Task<ActionResult<PayrollRunDto>> Create(CreatePayrollRunRequest request, CancellationToken ct)
    {
        try { return Ok(await _payrollRuns.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/recompute")]
    public async Task<ActionResult<PayrollRunDto>> Recompute(Guid id, CancellationToken ct)
    {
        try { return Ok(await _payrollRuns.RecomputeAsync(id, ct)); }
        catch (AppException ex) when (ex.Message == "Payroll run not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<PayrollRunDto>> Complete(Guid id, CancellationToken ct)
    {
        try { return Ok(await _payrollRuns.CompleteAsync(id, ct)); }
        catch (AppException ex) when (ex.Message == "Payroll run not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
