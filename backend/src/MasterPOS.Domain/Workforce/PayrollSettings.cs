using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Workforce;

/// <summary>
/// One row per Company — the Payroll Settings screen. Every statutory
/// toggle here is a business decision the company makes once (which
/// scheme they're registered under, whether OT is even paid), and
/// PayrollRunService reads this live at compute time — never a hardcoded
/// constant. PF and SSF are independent toggles here, not mutually
/// exclusive: real Nepali practice normally registers a company under
/// one scheme or the other, never both, but that's a business decision
/// left to the company, not something this table enforces.
/// </summary>
public class PayrollSettings : CompanyOwnedEntity
{
    public bool OvertimeEnabled { get; set; } = true;
    /// <summary>e.g. 1.5 = time-and-a-half. Ignored when OvertimeEnabled is false.</summary>
    public decimal OvertimeMultiplier { get; set; } = 1.5m;

    public bool PfEnabled { get; set; }
    /// <summary>% of Basic Salary withheld from the employee's own pay.</summary>
    public decimal PfEmployeePercent { get; set; } = 10m;
    /// <summary>% of Basic Salary the employer contributes — tracked for
    /// statutory reporting, never deducted from the employee's NetPay.</summary>
    public decimal PfEmployerPercent { get; set; } = 10m;

    public bool SsfEnabled { get; set; }
    public decimal SsfEmployeePercent { get; set; } = 11m;
    public decimal SsfEmployerPercent { get; set; } = 20m;

    /// <summary>Income tax withholding via the company's own TaxSlabs table.</summary>
    public bool TdsEnabled { get; set; }

    /// <summary>Gates the separate once-a-year FestivalBonus run type — not
    /// part of the monthly Payroll Run at all.</summary>
    public bool FestivalBonusEnabled { get; set; }
    /// <summary>% of Basic Salary — commonly 100% (one month's basic).</summary>
    public decimal FestivalBonusPercent { get; set; } = 100m;
}
