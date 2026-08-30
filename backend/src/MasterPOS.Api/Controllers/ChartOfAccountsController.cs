using MasterPOS.Application.Accounting;
using MasterPOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/accounting/chart-of-accounts")]
public class ChartOfAccountsController : ControllerBase
{
    private readonly IChartOfAccountService _accounts;

    public ChartOfAccountsController(IChartOfAccountService accounts) => _accounts = accounts;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChartOfAccountDto>>> List(CancellationToken ct)
        => Ok(await _accounts.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ChartOfAccountDto>> Create(UpsertChartOfAccountRequest request, CancellationToken ct)
    {
        try { return Ok(await _accounts.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ChartOfAccountDto>> Update(Guid id, UpsertChartOfAccountRequest request, CancellationToken ct)
    {
        try { return Ok(await _accounts.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Account not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _accounts.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) when (ex.Message == "Account not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("seed-defaults")]
    public async Task<ActionResult<IReadOnlyList<ChartOfAccountDto>>> SeedDefaults(CancellationToken ct)
    {
        try { return Ok(await _accounts.SeedDefaultsAsync(ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
