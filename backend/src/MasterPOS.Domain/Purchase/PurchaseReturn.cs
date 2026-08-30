using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Purchase;

/// <summary>Its own document type — not a negative invoice — so it prints
/// and reports as a distinct transaction, matching the "Purchase Return"
/// tab in the design.</summary>
public class PurchaseReturn : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string ReturnNumber { get; set; } = null!;
    public Guid? OriginalPurchaseInvoiceId { get; set; }
    public Guid SupplierId { get; set; }
    public DateOnly ReturnDate { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public decimal SubTotalAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrandTotalAmount { get; set; }
    public string? Narration { get; set; }

    public Branch Branch { get; set; } = null!;
    public PurchaseInvoice? OriginalPurchaseInvoice { get; set; }
    public Party Supplier { get; set; } = null!;
    public ICollection<PurchaseReturnLine> Lines { get; set; } = new List<PurchaseReturnLine>();
}
