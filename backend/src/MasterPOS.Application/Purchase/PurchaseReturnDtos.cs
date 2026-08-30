namespace MasterPOS.Application.Purchase;

public record PurchaseReturnLineDto(
    Guid Id, Guid ProductId, string ProductName, Guid UnitId, string UnitName,
    decimal Quantity, decimal Rate, decimal VatPercent, decimal LineAmount);

public record PurchaseReturnDto(
    Guid Id, string ReturnNumber, Guid SupplierId, string SupplierName, Guid? OriginalPurchaseInvoiceId,
    DateOnly ReturnDate, string Status,
    decimal SubTotalAmount, decimal VatAmount, decimal GrandTotalAmount, string? Narration,
    IReadOnlyList<PurchaseReturnLineDto> Lines);

public record CreatePurchaseReturnRequest(
    Guid SupplierId, Guid? OriginalPurchaseInvoiceId, DateOnly ReturnDate, string? Narration);

public record AddPurchaseReturnLineRequest(Guid ProductId, Guid UnitId, decimal Quantity, decimal Rate, decimal VatPercent);

public record UpdatePurchaseReturnLineRequest(Guid UnitId, decimal Quantity, decimal Rate, decimal VatPercent);
