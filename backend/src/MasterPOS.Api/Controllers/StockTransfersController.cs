using MasterPOS.Application.Common;
using MasterPOS.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory/transfers")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transfers;

    public StockTransfersController(IStockTransferService transfers) => _transfers = transfers;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockTransferDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await _transfers.ListAsync(status, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockTransferDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _transfers.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<StockTransferDto>> Create(CreateStockTransferRequest request, CancellationToken ct)
    {
        try { return Ok(await _transfers.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<StockTransferDto>> Post(Guid id, CancellationToken ct)
    {
        try { return Ok(await _transfers.PostAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<StockTransferDto>> Cancel(Guid id, CancellationToken ct)
    {
        try { return Ok(await _transfers.CancelAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
