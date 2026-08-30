using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Utility;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MasterPOS.Application.Utility;

/// <summary>
/// Runs a real T-SQL <c>BACKUP DATABASE</c> against this install's own SQL
/// Server — not a simulated log entry. The target directory
/// (<c>Backup:Directory</c> in appsettings, or the <c>Backup__Directory</c>
/// env var) must already exist and be writable by the SQL Server *service*
/// account, not the API process — same "local install, admin configures
/// it" model as the connection string and JWT signing key.
/// </summary>
public class BackupService : IBackupService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IConfiguration _config;

    public BackupService(MasterPosDbContext db, ICurrentUserContext currentUser, IConfiguration config)
    {
        _db = db;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<BackupLogEntryDto> TriggerAsync(CancellationToken ct = default)
    {
        var directory = _config["Backup:Directory"];
        if (string.IsNullOrWhiteSpace(directory))
            throw new AppException("No backup directory configured — set Backup:Directory in appsettings.json or the Backup__Directory environment variable.");

        var dbName = _db.Database.GetDbConnection().Database;
        var fileName = $"{dbName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
        var separator = directory.Contains('\\') ? "\\" : "/";
        var fullPath = directory.TrimEnd('/', '\\') + separator + fileName;

        try
        {
            // BACKUP DATABASE can't parameterize the database name (it's an
            // identifier, not a value) — bracket-escaped from the live
            // connection's own database name, not user input, so this isn't
            // taking untrusted text into the statement. The disk path is a
            // real parameter.
            // Built via concatenation, not string interpolation, so the disk
            // path stays a genuine {0} parameter EF Core substitutes safely —
            // an interpolated string here would (rightly) trip the analyzer
            // into thinking the path was being spliced in unparameterized.
            var escapedDbName = dbName.Replace("]", "]]");
            var sql = "BACKUP DATABASE [" + escapedDbName + "] TO DISK = {0} WITH INIT, STATS = 10";
            await _db.Database.ExecuteSqlRawAsync(sql, new object[] { fullPath }, ct);

            var sizes = await _db.Database
                .SqlQueryRaw<decimal>("SELECT TOP 1 backup_size FROM msdb.dbo.backupset WHERE database_name = {0} ORDER BY backup_start_date DESC", dbName)
                .ToListAsync(ct);
            long? sizeBytes = sizes.Count > 0 ? (long)Math.Round(sizes[0]) : null;

            var entry = new BackupLogEntry
            {
                CompanyId = _currentUser.CompanyId,
                FilePath = fullPath,
                SizeBytes = sizeBytes,
                TriggeredByUserId = _currentUser.UserId,
                Status = BackupStatus.Success,
            };
            _db.BackupLogEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
            return ToDto(entry);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _db.BackupLogEntries.Add(new BackupLogEntry
            {
                CompanyId = _currentUser.CompanyId,
                FilePath = fullPath,
                TriggeredByUserId = _currentUser.UserId,
                Status = BackupStatus.Failed,
            });
            await _db.SaveChangesAsync(ct);
            throw new AppException($"Backup failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<BackupLogEntryDto>> ListAsync(CancellationToken ct = default)
    {
        var entries = await _db.BackupLogEntries
            .Where(b => b.CompanyId == _currentUser.CompanyId)
            .OrderByDescending(b => b.BackupAtUtc)
            .ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    private static BackupLogEntryDto ToDto(BackupLogEntry b) => new(
        b.Id, b.BackupAtUtc, b.FilePath, b.SizeBytes, b.TriggeredByUserId, b.Status.ToString());
}
