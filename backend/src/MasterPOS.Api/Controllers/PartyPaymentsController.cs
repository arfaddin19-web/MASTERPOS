using MasterPOS.Application.Accounting;
using MasterPOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/accounting/party-payments")]
public class PartyPaymentsController : ControllerBase
{
    private readonly IPartyPaymentService _payments;

    public PartyPaymentsController(IPartyPaymentService payments) => _payments = payments;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PartyPaymentDto>>> List([FromQuery] Guid? partyId, CancellationToken ct)
        => Ok(await _payments.ListAsync(partyId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartyPaymentDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _payments.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<PartyPaymentDto>> Create(CreatePartyPaymentRequest request, CancellationToken ct)
    {
        try { return Ok(await _payments.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
