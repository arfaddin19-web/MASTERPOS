using MasterPOS.Application.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/utility/audit-log")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogQueryService _auditLog;

    public AuditLogController(IAuditLogQueryService auditLog) => _auditLog = auditLog;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> List(
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] string? entityType, CancellationToken ct)
        => Ok(await _auditLog.ListAsync(fromDate, toDate, entityType, ct));
}
