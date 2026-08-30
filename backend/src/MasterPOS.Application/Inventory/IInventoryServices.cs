namespace MasterPOS.Application.Inventory;

/// <summary>Single-item corrections (breakage, count mismatch, expiry
/// write-off) — no Draft/Posted lifecycle: creating one *is* posting it,
/// writing the matching StockLedgerEntry in the same call. See
/// StockAdjustment's class remarks.</summary>
public interface IStockAdjustmentService
{
    Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentRequest request, CancellationToken ct = default);
    Task<StockAdjustmentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockAdjustmentDto>> ListAsync(Guid? productId = null, Guid? warehouseId = null, CancellationToken ct = default);
}

/// <summary>The "Quick Stock Transfer" form — one product, one From/To
/// warehouse pair. Created Pending (no stock movement yet); Post writes a
/// TransferOut row at the source and a TransferIn row at the destination
/// in the same call; a Pending transfer can still be Cancelled.</summary>
public interface IStockTransferService
{
    Task<StockTransferDto> CreateAsync(CreateStockTransferRequest request, CancellationToken ct = default);
    Task<StockTransferDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransferDto>> ListAsync(string? status = null, CancellationToken ct = default);
    Task<StockTransferDto> PostAsync(Guid id, CancellationToken ct = default);
    Task<StockTransferDto> CancelAsync(Guid id, CancellationToken ct = default);
}

/// <summary>One-time starting balance per product/warehouse (unique pair —
/// enforced both here and by the database). Creating one immediately
/// writes the matching StockLedgerEntry; there's no edit afterward — a
/// mistake is corrected with a Stock Adjustment, not by rewriting history.</summary>
public interface IOpeningStockService
{
    Task<OpeningStockDto> CreateAsync(CreateOpeningStockRequest request, CancellationToken ct = default);
    Task<OpeningStockDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OpeningStockDto>> ListAsync(CancellationToken ct = default);
}

/// <summary>Read-only queries over StockLedgerEntries — the Stock
/// Register / Item Ledger screen and the Reorder Suggestions screen.</summary>
public interface IStockReportService
{
    Task<IReadOnlyList<StockLedgerEntryDto>> GetLedgerAsync(
        Guid? productId = null, Guid? warehouseId = null,
        DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);

    Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(Guid? warehouseId = null, CancellationToken ct = default);

    Task<IReadOnlyList<ReorderSuggestionDto>> GetReorderSuggestionsAsync(CancellationToken ct = default);
}
