using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Core;

/// <summary>
/// One row per local install today. The output of the First-Time Setup
/// wizard (Business Type + Payroll toggle + Tax Registration).
/// </summary>
public class Company : AuditableEntity
{
    public string Name { get; set; } = null!;
    public BusinessType BusinessType { get; set; }
    public bool PayrollEnabled { get; set; } = true;
    public TaxRegistrationType TaxRegistrationType { get; set; }
    public string? VatRegistrationNumber { get; set; }
    public decimal VatRatePercent { get; set; } = 13.00m;
    public string PrimaryCurrencyCode { get; set; } = "NPR";

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
