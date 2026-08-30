using MasterPOS.Application.Common;
using MasterPOS.Application.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workforce/payroll-settings")]
public class PayrollSettingsController : ControllerBase
{
    private readonly IPayrollSettingsService _settings;

    public PayrollSettingsController(IPayrollSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<ActionResult<PayrollSettingsDto>> Get(CancellationToken ct)
        => Ok(await _settings.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<PayrollSettingsDto>> Update(UpdatePayrollSettingsRequest request, CancellationToken ct)
    {
        try { return Ok(await _settings.UpdateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
