using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Accounting;

/// <summary>
/// The "Opening Balance (Party, Accounts)" transaction — exactly one of
/// PartyId/AccountId is set (enforced by CK_OpeningBalances_ExactlyOneTarget
/// in the database), so it shows as its own transaction in reports rather
/// than being silently baked into a master record.
/// </summary>
public class OpeningBalance : CompanyOwnedEntity
{
    public Guid? PartyId { get; set; }
    public Guid? AccountId { get; set; }
    public decimal Amount { get; set; }
    public BalanceType BalanceType { get; set; }
    public DateOnly AsOfDate { get; set; }

    public Party? Party { get; set; }
    public ChartOfAccount? Account { get; set; }
}
