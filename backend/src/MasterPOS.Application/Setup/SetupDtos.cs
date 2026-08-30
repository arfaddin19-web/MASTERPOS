namespace MasterPOS.Application.Setup;

/// <summary>
/// The First-Time Setup wizard's single submit — Business Type, Payroll
/// toggle and Tax Registration from the design phase, plus the primary
/// Branch and the initial Admin login it provisions.
/// </summary>
/// <param name="BusinessType">Enum member name: "Cafe" or "Trading".</param>
/// <param name="TaxRegistrationType">Enum member name: "Vat" or "Pan".</param>
public record SetupCompanyRequest(
    string CompanyName,
    string BusinessType,
    string TaxRegistrationType,
    string? VatRegistrationNumber,
    decimal VatRatePercent,
    bool PayrollEnabled,
    string BranchName,
    string? City,
    string? Address,
    string? Phone,
    string AdminFullName,
    string AdminUsername,
    string AdminPassword,
    string? AdminEmail);

public record SetupStatusResponse(bool IsSetupComplete);

public record SetupCompanyResponse(Guid CompanyId, Guid BranchId, Guid AdminUserId, Guid AdminRoleId);
