using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Purchase;

public class PurchaseReturnLine
{
    public Guid Id { get; set; }
    public Guid PurchaseReturnId { get; set; }
    public Guid ProductId { get; set; }
    public Guid UnitId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal VatPercent { get; set; }
    public decimal LineAmount { get; set; }

    public PurchaseReturn PurchaseReturn { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public UnitOfMeasure Unit { get; set; } = null!;
}
