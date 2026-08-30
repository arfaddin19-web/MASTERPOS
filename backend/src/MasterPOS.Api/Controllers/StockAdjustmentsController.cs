using MasterPOS.Application.Common;
using MasterPOS.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory/adjustments")]
public class StockAdjustmentsController : ControllerBase
{
    private readonly IStockAdjustmentService _adjustments;

    public StockAdjustmentsController(IStockAdjustmentService adjustments) => _adjustments = adjustments;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockAdjustmentDto>>> List(
        [FromQuery] Guid? productId, [FromQuery] Guid? warehouseId, CancellationToken ct)
        => Ok(await _adjustments.ListAsync(productId, warehouseId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockAdjustmentDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _adjustments.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<StockAdjustmentDto>> Create(CreateStockAdjustmentRequest request, CancellationToken ct)
    {
        try { return Ok(await _adjustments.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
