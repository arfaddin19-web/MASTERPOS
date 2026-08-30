using MasterPOS.Application.Common;
using MasterPOS.Application.Purchase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/purchase/invoices")]
public class PurchaseInvoicesController : ControllerBase
{
    private readonly IPurchaseInvoiceService _invoices;

    public PurchaseInvoicesController(IPurchaseInvoiceService invoices) => _invoices = invoices;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseInvoiceDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await _invoices.ListAsync(status, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseInvoiceDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _invoices.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseInvoiceDto>> Create(CreatePurchaseInvoiceRequest request, CancellationToken ct)
    {
        try { return Ok(await _invoices.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<PurchaseInvoiceDto>> AddLine(Guid id, AddPurchaseInvoiceLineRequest request, CancellationToken ct)
    {
        try { return Ok(await _invoices.AddLineAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<PurchaseInvoiceDto>> UpdateLine(Guid id, Guid lineId, UpdatePurchaseInvoiceLineRequest request, CancellationToken ct)
    {
        try { return Ok(await _invoices.UpdateLineAsync(id, lineId, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<PurchaseInvoiceDto>> RemoveLine(Guid id, Guid lineId, CancellationToken ct)
    {
        try { return Ok(await _invoices.RemoveLineAsync(id, lineId, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<PurchaseInvoiceDto>> Post(Guid id, CancellationToken ct)
    {
        try { return Ok(await _invoices.PostAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PurchaseInvoiceDto>> Cancel(Guid id, CancellationToken ct)
    {
        try { return Ok(await _invoices.CancelAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<PurchaseInvoiceDto>> RecordPayment(Guid id, RecordPurchasePaymentRequest request, CancellationToken ct)
    {
        try { return Ok(await _invoices.RecordPaymentAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
