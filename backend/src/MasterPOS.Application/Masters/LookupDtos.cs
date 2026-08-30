namespace MasterPOS.Application.Masters;

public record ProductCategoryDto(Guid Id, string Name, Guid? ParentCategoryId);
public record CreateProductCategoryRequest(string Name, Guid? ParentCategoryId);

public record ProductGroupDto(Guid Id, string Name);
public record CreateProductGroupRequest(string Name);

public record UnitDto(Guid Id, string Name, string? ShortCode);
public record CreateUnitRequest(string Name, string? ShortCode);

public record WarehouseDto(Guid Id, string Name, Guid BranchId, bool IsDefault);
public record CreateWarehouseRequest(string Name, Guid BranchId, bool IsDefault);
