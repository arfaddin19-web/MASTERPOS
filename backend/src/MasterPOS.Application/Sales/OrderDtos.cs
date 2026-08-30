namespace MasterPOS.Application.Sales;

/// <param name="OrderType">Enum member name: "DineIn", "Takeaway", "Delivery", or "Counter".</param>
public record CreateOrderRequest(string OrderType, Guid? TableId, int? GuestCount, Guid? CustomerId);

public record OrderLineDto(
    Guid Id, Guid ProductId, string ProductName, decimal Quantity, decimal UnitPrice,
    string? Note, string? KotStation, string KotStatus, decimal LineTotalAmount);

public record OrderPaymentDto(Guid Id, decimal Amount, string PaymentMode, string? PaidByLabel, DateTime PaidAtUtc);

public record OrderDto(
    Guid Id, string OrderNumber, string OrderType, Guid? TableId, string? TableNumber, int? GuestCount,
    Guid? CustomerId, string? CustomerName, string Status,
    decimal SubTotalAmount, decimal DiscountAmount, decimal VatAmount, decimal RoundOffAmount, decimal GrandTotalAmount,
    decimal AmountPaid, decimal AmountRemaining,
    DateTime OpenedAtUtc, DateTime? ClosedAtUtc,
    IReadOnlyList<OrderLineDto> Lines, IReadOnlyList<OrderPaymentDto> Payments);

public record AddOrderLineRequest(Guid ProductId, decimal Quantity, string? Note);

public record UpdateOrderLineRequest(decimal Quantity, string? Note);

/// <param name="PaymentMode">Enum member name: "Cash", "Card", "ESewa", "Khalti", or "BankTransfer".</param>
public record AddPaymentRequest(decimal Amount, string PaymentMode, string? PaidByLabel);

public record ApplyDiscountOfferRequest(Guid DiscountOfferId);

/// <param name="DiscountType">Enum member name: "Percent" or "Amount".</param>
public record ApplyManualDiscountRequest(string DiscountType, decimal Value);

public record KotPrintResultDto(string Station, int LineCount, bool IsReprint);
