namespace MasterPOS.Application.Utility;

public interface IPrinterService
{
    Task<PrinterDto> CreateAsync(UpsertPrinterRequest request, CancellationToken ct = default);
    Task<PrinterDto> UpdateAsync(Guid id, UpsertPrinterRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PrinterDto>> ListAsync(Guid? branchId = null, CancellationToken ct = default);
}

/// <summary>Settings → Payment Modes. All five modes are auto-created
/// (Cash/Card enabled, the rest off) the first time anyone asks, same
/// lazy-default pattern as Payroll Settings — no explicit init step.</summary>
public interface IPaymentModeSettingService
{
    Task<IReadOnlyList<PaymentModeSettingDto>> ListAsync(CancellationToken ct = default);
    Task<PaymentModeSettingDto> SetEnabledAsync(string code, SetPaymentModeEnabledRequest request, CancellationToken ct = default);
}

/// <summary>Settings → Audit Trail. Read-only — every write comes from
/// IAuditLogger, called from the other modules' own business-significant
/// moments, not from here.</summary>
public interface IAuditLogQueryService
{
    Task<IReadOnlyList<AuditLogEntryDto>> ListAsync(
        DateOnly? fromDate = null, DateOnly? toDate = null, string? entityType = null, CancellationToken ct = default);
}

/// <summary>Settings → Backup. TriggerAsync runs a real
/// <c>BACKUP DATABASE</c> against the install's own SQL Server — matches
/// the local-server-per-client deployment model (see the backend
/// README): the database being backed up is always this install's one
/// Company, on the same machine the API runs on.</summary>
public interface IBackupService
{
    Task<BackupLogEntryDto> TriggerAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BackupLogEntryDto>> ListAsync(CancellationToken ct = default);
}
