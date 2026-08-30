using MasterPOS.Application.Common;
using MasterPOS.Application.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workforce/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendance;

    public AttendanceController(IAttendanceService attendance) => _attendance = attendance;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttendanceDto>>> List(
        [FromQuery] Guid? employeeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
        => Ok(await _attendance.ListAsync(employeeId, fromDate, toDate, ct));

    [HttpGet("today")]
    public async Task<ActionResult<IReadOnlyList<TodayAttendanceRowDto>>> Today(CancellationToken ct)
    {
        try { return Ok(await _attendance.GetTodaySnapshotAsync(ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("check-in")]
    public async Task<ActionResult<AttendanceDto>> CheckIn(CheckInRequest request, CancellationToken ct)
    {
        try { return Ok(await _attendance.CheckInAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/check-out")]
    public async Task<ActionResult<AttendanceDto>> CheckOut(Guid id, CancellationToken ct)
    {
        try { return Ok(await _attendance.CheckOutAsync(id, ct)); }
        catch (AppException ex) when (ex.Message == "Attendance record not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("mark")]
    public async Task<ActionResult<AttendanceDto>> Mark(MarkAttendanceRequest request, CancellationToken ct)
    {
        try { return Ok(await _attendance.MarkAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
