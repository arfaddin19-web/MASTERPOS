namespace MasterPOS.Application.Utility;

public record PrinterDto(
    Guid Id, Guid BranchId, string BranchName, string Name, string PrinterType,
    string? Station, string? ConnectionInfo, bool IsEnabled);

public record UpsertPrinterRequest(
    Guid BranchId, string Name, string PrinterType, string? Station, string? ConnectionInfo, bool IsEnabled);

public record PaymentModeSettingDto(Guid Id, string Code, bool IsEnabled);

public record SetPaymentModeEnabledRequest(bool IsEnabled);

// ---- Audit Log (read-only) ----

public record AuditLogEntryDto(
    Guid Id, Guid UserId, string Action, string EntityType, Guid? EntityId, string Description, DateTime OccurredAtUtc);

// ---- Backups ----

public record BackupLogEntryDto(
    Guid Id, DateTime BackupAtUtc, string FilePath, long? SizeBytes, Guid? TriggeredByUserId, string Status);

