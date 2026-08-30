using MasterPOS.Application.Common;
using MasterPOS.Application.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workforce/advances")]
public class EmployeeAdvancesController : ControllerBase
{
    private readonly IEmployeeAdvanceService _advances;

    public EmployeeAdvancesController(IEmployeeAdvanceService advances) => _advances = advances;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeAdvanceDto>>> List(
        [FromQuery] Guid? employeeId, [FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await _advances.ListAsync(employeeId, status, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeAdvanceDto>> Create(CreateEmployeeAdvanceRequest request, CancellationToken ct)
    {
        try { return Ok(await _advances.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/recover")]
    public async Task<ActionResult<EmployeeAdvanceDto>> RecordRecovery(Guid id, RecordAdvanceRecoveryRequest request, CancellationToken ct)
    {
        try { return Ok(await _advances.RecordRecoveryAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Employee advance not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
