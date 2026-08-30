using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Inventory;
using MasterPOS.Domain.Masters;
using MasterPOS.Domain.Sales;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Sales;

public class OrderService : IOrderService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public OrderService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<OrderType>(request.OrderType, ignoreCase: true, out var orderType))
            throw new AppException($"Unknown order type '{request.OrderType}'.");

        var branchId = _currentUser.BranchId
            ?? throw new AppException("Your account has no default branch assigned.");

        DiningTable? table = null;
        if (request.TableId is { } tableId)
        {
            table = await _db.DiningTables.SingleOrDefaultAsync(
                t => t.Id == tableId && t.CompanyId == _currentUser.CompanyId && !t.IsDeleted, ct)
                ?? throw new AppException("The selected table does not exist.");
            if (table.Status != DiningTableStatus.Vacant)
                throw new AppException($"Table {table.TableNumber} is not vacant.");
        }

        if (request.CustomerId is { } customerId
            && !await _db.Parties.AnyAsync(p => p.Id == customerId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct))
            throw new AppException("The selected customer does not exist.");

        var order = new Order
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = branchId,
            OrderNumber = await GenerateOrderNumberAsync(ct),
            OrderType = orderType,
            TableId = request.TableId,
            GuestCount = request.GuestCount,
            CustomerId = request.CustomerId,
            CashierUserId = _currentUser.UserId,
            Status = OrderStatus.Open,
        };
        _db.Orders.Add(order);

        if (table is not null)
            table.Status = DiningTableStatus.Occupied;

        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedOrderAsync(order.Id, ct));
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedOrderAsync(id, ct));

    public async Task<IReadOnlyList<OrderDto>> ListOpenAsync(CancellationToken ct = default)
    {
        var orders = await _db.Orders
            .Include(o => o.Lines.Where(l => !l.IsDeleted)).ThenInclude(l => l.Product)
            .Include(o => o.Payments)
            .Include(o => o.Table)
            .Include(o => o.Customer)
            .Where(o => o.CompanyId == _currentUser.CompanyId && !o.IsDeleted
                && o.Status != OrderStatus.Paid && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.OpenedAtUtc)
            .ToListAsync(ct);
        return orders.Select(ToDto).ToList();
    }

    public async Task<OrderDto> AddLineAsync(Guid orderId, AddOrderLineRequest request, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        EnsureModifiable(order);

        if (request.Quantity <= 0)
            throw new AppException("Quantity must be greater than zero.");

        var product = await _db.Products.SingleOrDefaultAsync(
            p => p.Id == request.ProductId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct)
            ?? throw new AppException("The selected product does not exist.");

        // Consumable = internal use only, never sold; TrackInPos/IsActive are the merchant's
        // own POS-visibility toggles. See Product's class remarks for the full ProductType story.
        if (product.ProductType == ProductType.Consumable)
            throw new AppException($"'{product.Name}' is a Consumable — it's for internal use only and can't be sold.");
        if (!product.TrackInPos)
            throw new AppException($"'{product.Name}' is not enabled for POS.");
        if (!product.IsActive)
            throw new AppException($"'{product.Name}' is inactive.");

        // Snapshotted at add-time, deliberately — a later price or KOT-routing change on the
        // product master must not rewrite an already-punched order's history. See OrderLine.
        var line = new OrderLine
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = request.Quantity,
            UnitPrice = product.SalePrice,
            Note = request.Note,
            KotStation = product.KotStation,
            KotStatus = KotLineStatus.Pending,
            LineTotalAmount = request.Quantity * product.SalePrice,
        };
        _db.OrderLines.Add(line);
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(orderId, ct);
        return ToDto(await GetOwnedOrderAsync(orderId, ct));
    }

    public async Task<OrderDto> UpdateLineAsync(Guid orderId, Guid lineId, UpdateOrderLineRequest request, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        EnsureModifiable(order);

        if (request.Quantity <= 0)
            throw new AppException("Quantity must be greater than zero.");

        var line = order.Lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new AppException("Order line not found.");

        line.Quantity = request.Quantity;
        line.Note = request.Note;
        line.LineTotalAmount = request.Quantity * line.UnitPrice;
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(orderId, ct);
        return ToDto(await GetOwnedOrderAsync(orderId, ct));
    }

    public async Task<OrderDto> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        EnsureModifiable(order);

        var line = order.Lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new AppException("Order line not found.");
        line.IsDeleted = true;
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(orderId, ct);
        return ToDto(await GetOwnedOrderAsync(orderId, ct));
    }

    public async Task<OrderDto> ApplyDiscountOfferAsync(Guid orderId, ApplyDiscountOfferRequest request, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        EnsureModifiable(order);

        var offer = await _db.DiscountOffers.SingleOrDefaultAsync(
            o => o.Id == request.DiscountOfferId && o.CompanyId == _currentUser.CompanyId && !o.IsDeleted, ct)
            ?? throw new AppException("The selected discount offer does not exist.");
        if (!offer.IsActive)
            throw new AppException($"'{offer.Name}' is not active.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (offer.ValidFrom is { } from && today < from)
            throw new AppException($"'{offer.Name}' isn't valid until {from:yyyy-MM-dd}.");
        if (offer.ValidTo is { } to && today > to)
            throw new AppException($"'{offer.Name}' expired on {to:yyyy-MM-dd}.");

        return await SetDiscountAsync(order, offer.DiscountType, offer.Value, ct);
    }

    public async Task<OrderDto> ApplyManualDiscountAsync(Guid orderId, ApplyManualDiscountRequest request, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        EnsureModifiable(order);

        if (!Enum.TryParse<DiscountType>(request.DiscountType, ignoreCase: true, out var type))
            throw new AppException($"Unknown discount type '{request.DiscountType}'.");
        if (request.Value <= 0)
            throw new AppException("Value must be greater than zero.");
        if (type == DiscountType.Percent && request.Value > 100)
            throw new AppException("A Percent discount can't exceed 100.");

        return await SetDiscountAsync(order, type, request.Value, ct);
    }

    public async Task<OrderDto> ClearDiscountAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        EnsureModifiable(order);

        order.DiscountAmount = 0m;
        await _db.SaveChangesAsync(ct);
        await RecalculateTotalsAsync(orderId, ct);
        return ToDto(await GetOwnedOrderAsync(orderId, ct));
    }

    private async Task<OrderDto> SetDiscountAsync(Order order, DiscountType type, decimal value, CancellationToken ct)
    {
        var subTotal = order.Lines.Sum(l => l.LineTotalAmount);
        if (subTotal <= 0)
            throw new AppException("Add at least one item before applying a discount.");

        order.DiscountAmount = type == DiscountType.Percent
            ? Math.Round(subTotal * value / 100m, 2)
            : Math.Min(value, subTotal); // a flat discount can never exceed the bill itself
        await _db.SaveChangesAsync(ct);

        await RecalculateTotalsAsync(order.Id, ct);
        return ToDto(await GetOwnedOrderAsync(order.Id, ct));
    }

    public async Task<IReadOnlyList<KotPrintResultDto>> PrintKotAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        var pendingLines = order.Lines.Where(l => l.KotStatus == KotLineStatus.Pending && l.KotStation is not null).ToList();
        if (pendingLines.Count == 0)
            throw new AppException("Nothing new to send to the kitchen/bar.");

        var results = new List<KotPrintResultDto>();
        foreach (var group in pendingLines.GroupBy(l => l.KotStation!.Value))
        {
            var isReprint = await _db.KotPrintLogs.AnyAsync(k => k.OrderId == orderId && k.Station == group.Key, ct);
            _db.KotPrintLogs.Add(new KotPrintLog
            {
                OrderId = orderId,
                Station = group.Key,
                PrintedByUserId = _currentUser.UserId,
                IsReprint = isReprint,
            });
            foreach (var line in group)
                line.KotStatus = KotLineStatus.Sent;

            results.Add(new KotPrintResultDto(group.Key.ToString(), group.Count(), isReprint));
        }
        await _db.SaveChangesAsync(ct);
        return results;
    }

    public async Task<OrderDto> AddPaymentAsync(Guid orderId, AddPaymentRequest request, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        if (order.Status is OrderStatus.Paid or OrderStatus.Cancelled)
            throw new AppException($"Order {order.OrderNumber} is already closed.");
        if (order.Lines.Count == 0)
            throw new AppException("Add at least one item before taking payment.");
        if (!Enum.TryParse<PaymentMode>(request.PaymentMode, ignoreCase: true, out var mode))
            throw new AppException($"Unknown payment mode '{request.PaymentMode}'.");
        if (request.Amount <= 0)
            throw new AppException("Payment amount must be greater than zero.");

        var alreadyPaid = order.Payments.Sum(p => p.Amount);
        var remaining = order.GrandTotalAmount - alreadyPaid;
        if (request.Amount > remaining)
            throw new AppException($"Payment of Rs. {request.Amount:0.00} exceeds the remaining balance of Rs. {remaining:0.00}.");

        _db.OrderPayments.Add(new OrderPayment
        {
            OrderId = order.Id,
            Amount = request.Amount,
            PaymentMode = mode,
            PaidByLabel = request.PaidByLabel,
            CashierUserId = _currentUser.UserId,
        });
        await _db.SaveChangesAsync(ct);

        // Closing the order — Status → Paid — is the one moment stock actually moves (see
        // 06_Inventory.sql's StockLedgerEntries remarks: "... an Order closing ...").
        var totalPaid = alreadyPaid + request.Amount;
        if (totalPaid >= order.GrandTotalAmount)
        {
            order.Status = OrderStatus.Paid;
            order.ClosedAtUtc = DateTime.UtcNow;
            await DeductStockAsync(order, ct);
            if (order.Table is not null)
                order.Table.Status = DiningTableStatus.Vacant;
        }
        else
        {
            order.Status = OrderStatus.PartiallyPaid;
            if (order.Table is not null)
                order.Table.Status = DiningTableStatus.PartiallyPaid;
        }
        await _db.SaveChangesAsync(ct);
        if (order.Status == OrderStatus.Paid)
            await _auditLogger.LogAsync("Closed", "Sales.Orders", order.Id,
                $"closed order {order.OrderNumber} (Rs. {order.GrandTotalAmount:0.00})", ct);

        return ToDto(await GetOwnedOrderAsync(orderId, ct));
    }

    public async Task<OrderDto> HoldAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        EnsureModifiable(order);
        order.Status = OrderStatus.OnHold;
        await _db.SaveChangesAsync(ct);
        return ToDto(order);
    }

    public async Task<OrderDto> CancelAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(orderId, ct);
        if (order.Status == OrderStatus.Paid)
            throw new AppException($"Order {order.OrderNumber} is already paid and can't be cancelled.");
        if (order.Payments.Count > 0)
            throw new AppException($"Order {order.OrderNumber} has payments recorded — refund or remove them before cancelling.");

        order.Status = OrderStatus.Cancelled;
        order.ClosedAtUtc = DateTime.UtcNow;
        if (order.Table is not null)
            order.Table.Status = DiningTableStatus.Vacant;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Cancelled", "Sales.Orders", order.Id, $"cancelled order {order.OrderNumber}", ct);
        return ToDto(order);
    }

    /// <summary>
    /// Writes the actual stock movement for a closing order — the piece the Masters/POS/
    /// Inventory design checks all pointed at but that nothing enforced yet. Inventory lines
    /// deduct themselves; Recipe lines deduct their BOM components instead (scaled by the
    /// line's quantity); Service lines never touch stock.
    /// </summary>
    private async Task DeductStockAsync(Order order, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var line in order.Lines)
        {
            var product = line.Product;
            if (product.ProductType == ProductType.Service)
                continue;

            if (product.ProductType == ProductType.Recipe)
            {
                var bomLines = await _db.ProductBoms
                    .Include(b => b.ComponentProduct)
                    .Where(b => b.FinishedProductId == product.Id && !b.IsDeleted)
                    .ToListAsync(ct);
                foreach (var bomLine in bomLines)
                {
                    var warehouseId = bomLine.ComponentProduct.DefaultWarehouseId
                        ?? throw new AppException(
                            $"'{bomLine.ComponentProduct.Name}' has no default warehouse — set one before selling '{product.Name}'.");
                    _db.StockLedgerEntries.Add(new StockLedgerEntry
                    {
                        CompanyId = _currentUser.CompanyId,
                        WarehouseId = warehouseId,
                        ProductId = bomLine.ComponentProductId,
                        MovementDate = today,
                        QuantityOut = bomLine.Quantity * line.Quantity,
                        ReferenceType = StockReferenceType.Order,
                        ReferenceId = order.Id,
                        CreatedByUserId = _currentUser.UserId,
                    });
                }
            }
            else // Inventory
            {
                var warehouseId = product.DefaultWarehouseId
                    ?? throw new AppException($"'{product.Name}' has no default warehouse — set one before selling it.");
                _db.StockLedgerEntries.Add(new StockLedgerEntry
                {
                    CompanyId = _currentUser.CompanyId,
                    WarehouseId = warehouseId,
                    ProductId = product.Id,
                    MovementDate = today,
                    QuantityOut = line.Quantity,
                    ReferenceType = StockReferenceType.Order,
                    ReferenceId = order.Id,
                    CreatedByUserId = _currentUser.UserId,
                });
            }
        }
    }

    private async Task RecalculateTotalsAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Lines.Where(l => !l.IsDeleted)).ThenInclude(l => l.Product)
            .SingleAsync(o => o.Id == orderId, ct);
        var company = await _db.Companies.SingleAsync(c => c.Id == _currentUser.CompanyId, ct);

        var subTotal = order.Lines.Sum(l => l.LineTotalAmount);
        var vatable = order.Lines.Where(l => l.Product.IsVatApplicable).Sum(l => l.LineTotalAmount);

        // A discount reduces the taxable value before VAT, not after it (the
        // same principle Purchase's own line-level discount-then-VAT math
        // already follows) — even though this is stored as one order-level
        // DiscountAmount rather than a per-line one, so it's prorated across
        // the vatable/non-vatable portions of the bill before VAT is applied
        // to the now-smaller vatable base.
        var vatableDiscount = subTotal > 0 ? Math.Round(order.DiscountAmount * vatable / subTotal, 2) : 0m;
        var netVatable = vatable - vatableDiscount;
        var vat = Math.Round(netVatable * company.VatRatePercent / 100m, 2);
        var rawTotal = subTotal - order.DiscountAmount + vat;
        var roundedTotal = Math.Round(rawTotal, 0, MidpointRounding.AwayFromZero);

        order.SubTotalAmount = subTotal;
        order.VatAmount = vat;
        order.RoundOffAmount = roundedTotal - rawTotal;
        order.GrandTotalAmount = roundedTotal;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        // "ORD-10231" style — highest existing numeric suffix for this company, plus one.
        // Single local install (see the database README's deployment model), so there's no
        // cross-machine race to worry about.
        var companyId = _currentUser.CompanyId;
        var numbers = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.OrderNumber.StartsWith("ORD-"))
            .Select(o => o.OrderNumber)
            .ToListAsync(ct);

        var next = numbers
            .Select(n => int.TryParse(n.AsSpan(4), out var num) ? num : 0)
            .DefaultIfEmpty(10000)
            .Max() + 1;

        return $"ORD-{next}";
    }

    private static void EnsureModifiable(Order order)
    {
        if (order.Status is OrderStatus.Paid or OrderStatus.Cancelled)
            throw new AppException($"Order {order.OrderNumber} is closed and can no longer be changed.");
    }

    private async Task<Order> GetOwnedOrderAsync(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Lines.Where(l => !l.IsDeleted)).ThenInclude(l => l.Product)
            .Include(o => o.Payments)
            .Include(o => o.Table)
            .Include(o => o.Customer)
            .SingleOrDefaultAsync(o => o.Id == id && o.CompanyId == _currentUser.CompanyId && !o.IsDeleted, ct);
        return order ?? throw new AppException("Order not found.");
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.OrderNumber, o.OrderType.ToString(), o.TableId, o.Table?.TableNumber, o.GuestCount,
        o.CustomerId, o.Customer?.Name, o.Status.ToString(),
        o.SubTotalAmount, o.DiscountAmount, o.VatAmount, o.RoundOffAmount, o.GrandTotalAmount,
        o.AmountPaid, o.AmountRemaining,
        o.OpenedAtUtc, o.ClosedAtUtc,
        o.Lines.Where(l => !l.IsDeleted).Select(l => new OrderLineDto(
            l.Id, l.ProductId, l.Product.Name, l.Quantity, l.UnitPrice, l.Note,
            l.KotStation?.ToString(), l.KotStatus.ToString(), l.LineTotalAmount)).ToList(),
        o.Payments.Select(p => new OrderPaymentDto(p.Id, p.Amount, p.PaymentMode.ToString(), p.PaidByLabel, p.PaidAtUtc)).ToList());
}
