using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Workforce;

public class PayrollRunLine
{
    public Guid Id { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal BasicAmount { get; set; }
    public decimal AllowancesAmount { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal DeductionsAmount { get; set; }
    public decimal AdvanceDeductionAmount { get; set; }
    /// <summary>Employee-side contribution — reduces NetPayAmount.</summary>
    public decimal PfEmployeeAmount { get; set; }
    /// <summary>Employer-side contribution — informational only, never
    /// subtracted from the employee's own pay.</summary>
    public decimal PfEmployerAmount { get; set; }
    public decimal SsfEmployeeAmount { get; set; }
    public decimal SsfEmployerAmount { get; set; }
    /// <summary>Withheld income tax, from the company's TaxSlabs table —
    /// zero whenever PayrollSettings.TdsEnabled is off.</summary>
    public decimal TdsAmount { get; set; }
    public decimal NetPayAmount { get; set; }
    public PayrollLineStatus LineStatus { get; set; } = PayrollLineStatus.Ready;

    public PayrollRun PayrollRun { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
