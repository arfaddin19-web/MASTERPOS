using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Accounting;

/// <summary>
/// The "Payment Entry" transaction — settling a party's outstanding
/// balance, independent of a specific POS order (that's Sales.OrderPayment
/// instead). ReferenceType/ReferenceId optionally ties a payment to the
/// invoice it's settling.
/// </summary>
public class PartyPayment : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public Guid PartyId { get; set; }
    public PartyPaymentDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public PartyPaymentReferenceType? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Narration { get; set; }

    public Branch Branch { get; set; } = null!;
    public Party Party { get; set; } = null!;
}
