using MasterPOS.Application.Common;
using MasterPOS.Application.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/utility/payment-modes")]
public class PaymentModesController : ControllerBase
{
    private readonly IPaymentModeSettingService _modes;

    public PaymentModesController(IPaymentModeSettingService modes) => _modes = modes;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentModeSettingDto>>> List(CancellationToken ct)
        => Ok(await _modes.ListAsync(ct));

    [HttpPatch("{code}")]
    public async Task<ActionResult<PaymentModeSettingDto>> SetEnabled(string code, SetPaymentModeEnabledRequest request, CancellationToken ct)
    {
        try { return Ok(await _modes.SetEnabledAsync(code, request, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }
}
