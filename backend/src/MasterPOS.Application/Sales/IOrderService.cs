namespace MasterPOS.Application.Sales;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Orders still on the floor/counter — everything except Paid and Cancelled.
    /// What the POS screen's table grid and order tabs are built from.</summary>
    Task<IReadOnlyList<OrderDto>> ListOpenAsync(CancellationToken ct = default);

    Task<OrderDto> AddLineAsync(Guid orderId, AddOrderLineRequest request, CancellationToken ct = default);
    Task<OrderDto> UpdateLineAsync(Guid orderId, Guid lineId, UpdateOrderLineRequest request, CancellationToken ct = default);
    Task<OrderDto> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken ct = default);

    /// <summary>Applies a saved Discount Offer's current Value/DiscountType —
    /// rejecting one that's inactive or outside its Valid From/To window.</summary>
    Task<OrderDto> ApplyDiscountOfferAsync(Guid orderId, ApplyDiscountOfferRequest request, CancellationToken ct = default);
    /// <summary>An ad-hoc discount not tied to any saved offer — a
    /// manager's one-off override.</summary>
    Task<OrderDto> ApplyManualDiscountAsync(Guid orderId, ApplyManualDiscountRequest request, CancellationToken ct = default);
    Task<OrderDto> ClearDiscountAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Prints (or reprints) the KOT for every station with lines still Pending —
    /// only the lines added since the last print go out, per station.</summary>
    Task<IReadOnlyList<KotPrintResultDto>> PrintKotAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Records one Split-Payment entry. Crosses into Paid the moment the sum of
    /// payments reaches the grand total — which is also the moment stock actually moves
    /// (Recipe lines deduct their BOM components, Inventory lines deduct themselves;
    /// Service/Consumable lines never do — see Product's class remarks).</summary>
    Task<OrderDto> AddPaymentAsync(Guid orderId, AddPaymentRequest request, CancellationToken ct = default);

    Task<OrderDto> HoldAsync(Guid orderId, CancellationToken ct = default);
    Task<OrderDto> CancelAsync(Guid orderId, CancellationToken ct = default);
}
