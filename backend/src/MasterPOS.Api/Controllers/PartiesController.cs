using MasterPOS.Application.Common;
using MasterPOS.Application.Masters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/masters/parties")]
public class PartiesController : ControllerBase
{
    private readonly IPartyService _parties;

    public PartiesController(IPartyService parties) => _parties = parties;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PartyDto>>> List(
        [FromQuery] string? partyType, [FromQuery] bool activeOnly, CancellationToken ct)
    {
        try { return Ok(await _parties.ListAsync(partyType, activeOnly, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartyDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _parties.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<PartyDto>> Create(UpsertPartyRequest request, CancellationToken ct)
    {
        try { return Ok(await _parties.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PartyDto>> Update(Guid id, UpsertPartyRequest request, CancellationToken ct)
    {
        try { return Ok(await _parties.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Party not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) when (ex.Message.Contains("transaction history")) { return Conflict(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<PartyDto>> SetActive(Guid id, SetPartyActiveRequest request, CancellationToken ct)
    {
        try { return Ok(await _parties.SetActiveAsync(id, request, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _parties.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) when (ex.Message == "Party not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return Conflict(new { message = ex.Message }); }
    }
}
