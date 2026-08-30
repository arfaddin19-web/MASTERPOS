namespace MasterPOS.Application.Purchase;

public record PurchaseInvoiceLineDto(
    Guid Id, Guid ProductId, string ProductName, Guid UnitId, string UnitName,
    decimal Quantity, decimal Rate, decimal DiscountPercent, decimal VatPercent, decimal LineAmount);

public record PurchaseInvoiceDto(
    Guid Id, string InvoiceNumber, Guid SupplierId, string SupplierName, string? SupplierReferenceNo,
    DateOnly InvoiceDate, string? PaymentTerms, string Status,
    decimal SubTotalAmount, decimal DiscountAmount, decimal VatAmount, decimal RoundOffAmount, decimal GrandTotalAmount,
    decimal AmountPaid, decimal AmountRemaining, string? Narration,
    IReadOnlyList<PurchaseInvoiceLineDto> Lines);

public record CreatePurchaseInvoiceRequest(
    Guid SupplierId, string? SupplierReferenceNo, DateOnly InvoiceDate, string? PaymentTerms, string? Narration);

public record AddPurchaseInvoiceLineRequest(
    Guid ProductId, Guid UnitId, decimal Quantity, decimal Rate, decimal DiscountPercent, decimal VatPercent);

public record UpdatePurchaseInvoiceLineRequest(
    Guid UnitId, decimal Quantity, decimal Rate, decimal DiscountPercent, decimal VatPercent);

public record RecordPurchasePaymentRequest(decimal Amount);
