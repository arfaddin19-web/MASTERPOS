using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

/// <summary>Backs the Ledger and Final Account reports (Trial Balance,
/// P&amp;L, Balance Sheet).</summary>
public class ChartOfAccount : CompanyOwnedEntity
{
    public Guid? ParentAccountId { get; set; }
    public string Name { get; set; } = null!;
    public AccountType AccountType { get; set; }
    public bool IsSystemAccount { get; set; }

    public ChartOfAccount? ParentAccount { get; set; }
}
