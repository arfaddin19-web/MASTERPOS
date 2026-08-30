using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Workforce;

/// <summary>
/// One band of Nepal's progressive individual income-tax table — the
/// government revises the thresholds and rates almost every fiscal year,
/// so this is a company-editable table, not a constant in code.
/// TaxSlabService seeds a commonly-cited recent structure at first use;
/// that seed is a starting point for the admin to verify against the
/// current official rates before going live, not a guarantee of them.
/// </summary>
public class TaxSlab : CompanyOwnedEntity
{
    public MaritalStatus MaritalStatus { get; set; }
    /// <summary>Annual taxable income, inclusive.</summary>
    public decimal LowerBound { get; set; }
    /// <summary>Annual taxable income, inclusive. Null = no upper bound —
    /// the top band.</summary>
    public decimal? UpperBound { get; set; }
    public decimal RatePercent { get; set; }
}
