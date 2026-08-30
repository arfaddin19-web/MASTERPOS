using MasterPOS.Application.Common;
using MasterPOS.Application.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/utility/backups")]
public class BackupsController : ControllerBase
{
    private readonly IBackupService _backups;

    public BackupsController(IBackupService backups) => _backups = backups;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BackupLogEntryDto>>> List(CancellationToken ct)
        => Ok(await _backups.ListAsync(ct));

    /// <summary>Settings → Backup's "Run Backup Now" button.</summary>
    [HttpPost]
    public async Task<ActionResult<BackupLogEntryDto>> Trigger(CancellationToken ct)
    {
        try { return Ok(await _backups.TriggerAsync(ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
