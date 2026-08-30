using MasterPOS.Application.Common;
using MasterPOS.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory/opening-stock")]
public class OpeningStockController : ControllerBase
{
    private readonly IOpeningStockService _openingStock;

    public OpeningStockController(IOpeningStockService openingStock) => _openingStock = openingStock;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OpeningStockDto>>> List(CancellationToken ct)
        => Ok(await _openingStock.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OpeningStockDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _openingStock.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<OpeningStockDto>> Create(CreateOpeningStockRequest request, CancellationToken ct)
    {
        try { return Ok(await _openingStock.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
