using MasterPOS.Application.Common;
using MasterPOS.Application.Purchase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/purchase/returns")]
public class PurchaseReturnsController : ControllerBase
{
    private readonly IPurchaseReturnService _returns;

    public PurchaseReturnsController(IPurchaseReturnService returns) => _returns = returns;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseReturnDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await _returns.ListAsync(status, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseReturnDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _returns.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseReturnDto>> Create(CreatePurchaseReturnRequest request, CancellationToken ct)
    {
        try { return Ok(await _returns.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<PurchaseReturnDto>> AddLine(Guid id, AddPurchaseReturnLineRequest request, CancellationToken ct)
    {
        try { return Ok(await _returns.AddLineAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<PurchaseReturnDto>> UpdateLine(Guid id, Guid lineId, UpdatePurchaseReturnLineRequest request, CancellationToken ct)
    {
        try { return Ok(await _returns.UpdateLineAsync(id, lineId, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<PurchaseReturnDto>> RemoveLine(Guid id, Guid lineId, CancellationToken ct)
    {
        try { return Ok(await _returns.RemoveLineAsync(id, lineId, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<PurchaseReturnDto>> Post(Guid id, CancellationToken ct)
    {
        try { return Ok(await _returns.PostAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PurchaseReturnDto>> Cancel(Guid id, CancellationToken ct)
    {
        try { return Ok(await _returns.CancelAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
