using MasterPOS.Application.Auth;
using MasterPOS.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.LoginAsync(request, ct));
        }
        catch (AppException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}
