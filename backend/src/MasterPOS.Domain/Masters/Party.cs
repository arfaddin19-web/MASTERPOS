using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

/// <summary>
/// Unifies "Party Master" and "Customer Master" from the design —
/// PartyType says which the record is; the loyalty fields are only
/// meaningful (and only shown by the app) when PartyType includes
/// Customer.
/// </summary>
public class Party : CompanyOwnedEntity
{
    public PartyType PartyType { get; set; }
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? VatOrPanNumber { get; set; }
    public decimal OpeningBalanceAmount { get; set; }
    public BalanceType OpeningBalanceType { get; set; } = BalanceType.Dr;
    public int LoyaltyPoints { get; set; }
    public bool IsActive { get; set; } = true;
}
