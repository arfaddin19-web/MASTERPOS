using MasterPOS.Domain.Common;

namespace MasterPOS.Domain.Masters;

public class DiscountOffer : CompanyOwnedEntity
{
    public string Name { get; set; } = null!;
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}
