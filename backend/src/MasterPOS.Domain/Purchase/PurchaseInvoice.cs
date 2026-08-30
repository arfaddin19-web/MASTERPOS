using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Purchase;

public class PurchaseInvoice : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public string? SupplierReferenceNo { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public string? PaymentTerms { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public decimal SubTotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal RoundOffAmount { get; set; }
    public decimal GrandTotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string? Narration { get; set; }

    public Branch Branch { get; set; } = null!;
    public Party Supplier { get; set; } = null!;
    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
}
