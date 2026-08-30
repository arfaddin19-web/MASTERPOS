namespace MasterPOS.Application.Masters;

public record ProductDto(
    Guid Id,
    string Name,
    string ProductType,
    Guid? CategoryId,
    string? CategoryName,
    Guid? GroupId,
    string? GroupName,
    Guid UnitId,
    string UnitName,
    Guid? DefaultWarehouseId,
    string? DefaultWarehouseName,
    string? Barcode,
    decimal PurchasePrice,
    decimal SalePrice,
    bool IsVatApplicable,
    decimal ReorderLevel,
    string? KotStation,
    int? PrepTimeMinutes,
    bool TrackInPos,
    bool IsActive);

/// <param name="ProductType">Enum member name: "Inventory", "Service", "Recipe", or "Consumable".</param>
/// <param name="KotStation">Enum member name "Kitchen"/"Bar", or null.</param>
public record UpsertProductRequest(
    string Name,
    string ProductType,
    Guid? CategoryId,
    Guid? GroupId,
    Guid UnitId,
    Guid? DefaultWarehouseId,
    string? Barcode,
    decimal PurchasePrice,
    decimal SalePrice,
    bool IsVatApplicable,
    decimal ReorderLevel,
    string? KotStation,
    int? PrepTimeMinutes,
    bool TrackInPos,
    bool IsActive);

public record SetProductActiveRequest(bool IsActive);

public record ProductBomLineDto(Guid ComponentProductId, string ComponentProductName, string UnitName, decimal Quantity);

public record SetProductBomLine(Guid ComponentProductId, decimal Quantity);

public record SetProductBomRequest(IReadOnlyList<SetProductBomLine> Lines);
