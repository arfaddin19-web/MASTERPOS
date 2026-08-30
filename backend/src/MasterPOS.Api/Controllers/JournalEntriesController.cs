using MasterPOS.Application.Accounting;
using MasterPOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/accounting/journal-entries")]
public class JournalEntriesController : ControllerBase
{
    private readonly IJournalEntryService _entries;

    public JournalEntriesController(IJournalEntryService entries) => _entries = entries;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JournalEntryDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await _entries.ListAsync(status, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JournalEntryDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _entries.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<JournalEntryDto>> Create(CreateJournalEntryRequest request, CancellationToken ct)
    {
        try { return Ok(await _entries.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<JournalEntryDto>> AddLine(Guid id, AddJournalEntryLineRequest request, CancellationToken ct)
    {
        try { return Ok(await _entries.AddLineAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<JournalEntryDto>> RemoveLine(Guid id, Guid lineId, CancellationToken ct)
    {
        try { return Ok(await _entries.RemoveLineAsync(id, lineId, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<JournalEntryDto>> Post(Guid id, CancellationToken ct)
    {
        try { return Ok(await _entries.PostAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<JournalEntryDto>> Cancel(Guid id, CancellationToken ct)
    {
        try { return Ok(await _entries.CancelAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
