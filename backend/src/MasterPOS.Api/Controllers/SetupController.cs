using MasterPOS.Application.Common;
using MasterPOS.Application.Setup;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly ISetupService _setupService;

    public SetupController(ISetupService setupService) => _setupService = setupService;

    /// <summary>
    /// The client's Login screen calls this first: an incomplete setup
    /// routes to the First-Time Setup wizard instead of the login form.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken ct)
        => Ok(await _setupService.GetStatusAsync(ct));

    [HttpPost]
    public async Task<ActionResult<SetupCompanyResponse>> CompleteSetup(SetupCompanyRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _setupService.CompleteSetupAsync(request, ct));
        }
        catch (AppException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
