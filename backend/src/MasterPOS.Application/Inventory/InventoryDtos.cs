namespace MasterPOS.Application.Inventory;

// ---- Stock Adjustment ----

public record StockAdjustmentDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    Guid ProductId,
    string ProductName,
    decimal QuantityChange,
    string Reason,
    DateOnly AdjustmentDate);

public record CreateStockAdjustmentRequest(
    Guid WarehouseId,
    Guid ProductId,
    decimal QuantityChange,
    string Reason,
    DateOnly AdjustmentDate);

// ---- Stock Transfer ----

public record StockTransferDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid FromWarehouseId,
    string FromWarehouseName,
    Guid ToWarehouseId,
    string ToWarehouseName,
    decimal Quantity,
    DateOnly TransferDate,
    string Status);

public record CreateStockTransferRequest(
    Guid ProductId,
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    decimal Quantity,
    DateOnly TransferDate);

// ---- Opening Stock ----

public record OpeningStockDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitCost,
    DateOnly AsOfDate);

public record CreateOpeningStockRequest(
    Guid WarehouseId,
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost,
    DateOnly AsOfDate);

// ---- Stock reports (read-only) ----

public record StockLedgerEntryDto(
    Guid Id,
    DateOnly MovementDate,
    Guid ProductId,
    string ProductName,
    Guid WarehouseId,
    string WarehouseName,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal RunningBalance,
    string ReferenceType,
    Guid ReferenceId);

public record StockBalanceDto(
    Guid ProductId,
    string ProductName,
    Guid WarehouseId,
    string WarehouseName,
    decimal Balance);

public record ReorderSuggestionDto(
    Guid ProductId,
    string ProductName,
    decimal ReorderLevel,
    decimal CurrentBalance,
    decimal ShortBy);
