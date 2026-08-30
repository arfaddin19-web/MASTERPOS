using MasterPOS.Application.Common;
using MasterPOS.Application.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workforce/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _leave;

    public LeaveRequestsController(ILeaveRequestService leave) => _leave = leave;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> List(
        [FromQuery] Guid? employeeId, [FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await _leave.ListAsync(employeeId, status, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<LeaveRequestDto>> Create(CreateLeaveRequestRequest request, CancellationToken ct)
    {
        try { return Ok(await _leave.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<LeaveRequestDto>> Approve(Guid id, CancellationToken ct)
    {
        try { return Ok(await _leave.ApproveAsync(id, ct)); }
        catch (AppException ex) when (ex.Message == "Leave request not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<LeaveRequestDto>> Reject(Guid id, CancellationToken ct)
    {
        try { return Ok(await _leave.RejectAsync(id, ct)); }
        catch (AppException ex) when (ex.Message == "Leave request not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<LeaveRequestDto>> Cancel(Guid id, CancellationToken ct)
    {
        try { return Ok(await _leave.CancelAsync(id, ct)); }
        catch (AppException ex) when (ex.Message == "Leave request not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
