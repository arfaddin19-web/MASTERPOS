using MasterPOS.Application.Common;
using MasterPOS.Application.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workforce/tax-slabs")]
public class TaxSlabsController : ControllerBase
{
    private readonly ITaxSlabService _slabs;

    public TaxSlabsController(ITaxSlabService slabs) => _slabs = slabs;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaxSlabDto>>> List(CancellationToken ct)
        => Ok(await _slabs.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TaxSlabDto>> Create(UpsertTaxSlabRequest request, CancellationToken ct)
    {
        try { return Ok(await _slabs.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaxSlabDto>> Update(Guid id, UpsertTaxSlabRequest request, CancellationToken ct)
    {
        try { return Ok(await _slabs.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Tax slab not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _slabs.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("seed-defaults")]
    public async Task<ActionResult<IReadOnlyList<TaxSlabDto>>> SeedDefaults(CancellationToken ct)
    {
        try { return Ok(await _slabs.SeedDefaultsAsync(ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
