using MasterPOS.Application.Common;
using MasterPOS.Application.Masters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/masters/tables")]
public class DiningTablesController : ControllerBase
{
    private readonly IDiningTableService _tables;

    public DiningTablesController(IDiningTableService tables) => _tables = tables;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiningTableDto>>> List([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _tables.ListAsync(branchId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DiningTableDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _tables.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<DiningTableDto>> Create(CreateDiningTableRequest request, CancellationToken ct)
    {
        try { return Ok(await _tables.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DiningTableDto>> Update(Guid id, UpdateDiningTableRequest request, CancellationToken ct)
    {
        try { return Ok(await _tables.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Dining table not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _tables.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) when (ex.Message == "Dining table not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return Conflict(new { message = ex.Message }); }
    }
}
