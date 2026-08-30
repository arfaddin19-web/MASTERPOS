using MasterPOS.Application.Auth;
using MasterPOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/auth/roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roles;

    public RolesController(IRoleService roles) => _roles = roles;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct)
        => Ok(await _roles.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _roles.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create(UpsertRoleRequest request, CancellationToken ct)
    {
        try { return Ok(await _roles.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Update(Guid id, UpsertRoleRequest request, CancellationToken ct)
    {
        try { return Ok(await _roles.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Role not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _roles.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) when (ex.Message == "Role not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return Conflict(new { message = ex.Message }); }
    }
}
