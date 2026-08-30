using MasterPOS.Application.Common;
using MasterPOS.Application.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/utility/printers")]
public class PrintersController : ControllerBase
{
    private readonly IPrinterService _printers;

    public PrintersController(IPrinterService printers) => _printers = printers;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PrinterDto>>> List([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _printers.ListAsync(branchId, ct));

    [HttpPost]
    public async Task<ActionResult<PrinterDto>> Create(UpsertPrinterRequest request, CancellationToken ct)
    {
        try { return Ok(await _printers.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PrinterDto>> Update(Guid id, UpsertPrinterRequest request, CancellationToken ct)
    {
        try { return Ok(await _printers.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Printer not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _printers.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }
}
