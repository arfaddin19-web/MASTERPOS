using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;

namespace MasterPOS.Domain.Workforce;

public class PayrollRun : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public byte PeriodMonth { get; set; }
    public short PeriodYear { get; set; }
    public PayrollRunType RunType { get; set; } = PayrollRunType.Monthly;
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public DateTime? RunAtUtc { get; set; }

    public Branch Branch { get; set; } = null!;
    public ICollection<PayrollRunLine> Lines { get; set; } = new List<PayrollRunLine>();
}
