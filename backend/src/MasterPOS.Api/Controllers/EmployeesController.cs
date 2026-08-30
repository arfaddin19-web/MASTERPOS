using MasterPOS.Application.Common;
using MasterPOS.Application.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workforce/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employees;

    public EmployeesController(IEmployeeService employees) => _employees = employees;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List([FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _employees.ListAsync(activeOnly, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _employees.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken ct)
    {
        try { return Ok(await _employees.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, UpdateEmployeeRequest request, CancellationToken ct)
    {
        try { return Ok(await _employees.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Employee not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<EmployeeDto>> SetActive(Guid id, SetEmployeeActiveRequest request, CancellationToken ct)
    {
        try { return Ok(await _employees.SetActiveAsync(id, request, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _employees.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) when (ex.Message == "Employee not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return Conflict(new { message = ex.Message }); }
    }
}
