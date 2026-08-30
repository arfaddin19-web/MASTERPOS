using MasterPOS.Application.Accounting;
using MasterPOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/accounting/opening-balances")]
public class OpeningBalancesController : ControllerBase
{
    private readonly IOpeningBalanceService _balances;

    public OpeningBalancesController(IOpeningBalanceService balances) => _balances = balances;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OpeningBalanceDto>>> List(CancellationToken ct)
        => Ok(await _balances.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<OpeningBalanceDto>> Create(UpsertOpeningBalanceRequest request, CancellationToken ct)
    {
        try { return Ok(await _balances.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OpeningBalanceDto>> Update(Guid id, UpsertOpeningBalanceRequest request, CancellationToken ct)
    {
        try { return Ok(await _balances.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Opening balance not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _balances.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }
}
