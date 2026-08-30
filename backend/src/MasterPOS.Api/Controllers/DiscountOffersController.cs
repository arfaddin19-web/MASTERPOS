using MasterPOS.Application.Common;
using MasterPOS.Application.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sales/discount-offers")]
public class DiscountOffersController : ControllerBase
{
    private readonly IDiscountOfferService _offers;

    public DiscountOffersController(IDiscountOfferService offers) => _offers = offers;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiscountOfferDto>>> List([FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _offers.ListAsync(activeOnly, ct));

    [HttpPost]
    public async Task<ActionResult<DiscountOfferDto>> Create(UpsertDiscountOfferRequest request, CancellationToken ct)
    {
        try { return Ok(await _offers.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DiscountOfferDto>> Update(Guid id, UpsertDiscountOfferRequest request, CancellationToken ct)
    {
        try { return Ok(await _offers.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Discount offer not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<DiscountOfferDto>> SetActive(Guid id, SetDiscountOfferActiveRequest request, CancellationToken ct)
    {
        try { return Ok(await _offers.SetActiveAsync(id, request, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _offers.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }
}
