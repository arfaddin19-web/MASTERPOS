using MasterPOS.Application.Common;
using MasterPOS.Application.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sales/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> ListOpen(CancellationToken ct)
        => Ok(await _orders.ListOpenAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _orders.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        try { return Ok(await _orders.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<OrderDto>> AddLine(Guid id, AddOrderLineRequest request, CancellationToken ct)
    {
        try { return Ok(await _orders.AddLineAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<OrderDto>> UpdateLine(Guid id, Guid lineId, UpdateOrderLineRequest request, CancellationToken ct)
    {
        try { return Ok(await _orders.UpdateLineAsync(id, lineId, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<OrderDto>> RemoveLine(Guid id, Guid lineId, CancellationToken ct)
    {
        try { return Ok(await _orders.RemoveLineAsync(id, lineId, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/discount/offer")]
    public async Task<ActionResult<OrderDto>> ApplyDiscountOffer(Guid id, ApplyDiscountOfferRequest request, CancellationToken ct)
    {
        try { return Ok(await _orders.ApplyDiscountOfferAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/discount/manual")]
    public async Task<ActionResult<OrderDto>> ApplyManualDiscount(Guid id, ApplyManualDiscountRequest request, CancellationToken ct)
    {
        try { return Ok(await _orders.ApplyManualDiscountAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}/discount")]
    public async Task<ActionResult<OrderDto>> ClearDiscount(Guid id, CancellationToken ct)
    {
        try { return Ok(await _orders.ClearDiscountAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/kot")]
    public async Task<ActionResult<IReadOnlyList<KotPrintResultDto>>> PrintKot(Guid id, CancellationToken ct)
    {
        try { return Ok(await _orders.PrintKotAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<OrderDto>> AddPayment(Guid id, AddPaymentRequest request, CancellationToken ct)
    {
        try { return Ok(await _orders.AddPaymentAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/hold")]
    public async Task<ActionResult<OrderDto>> Hold(Guid id, CancellationToken ct)
    {
        try { return Ok(await _orders.HoldAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken ct)
    {
        try { return Ok(await _orders.CancelAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
