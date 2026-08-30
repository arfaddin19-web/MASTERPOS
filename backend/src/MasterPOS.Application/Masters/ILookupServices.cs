namespace MasterPOS.Application.Masters;

public interface IProductCategoryService
{
    Task<IReadOnlyList<ProductCategoryDto>> ListAsync(CancellationToken ct = default);
    Task<ProductCategoryDto> CreateAsync(CreateProductCategoryRequest request, CancellationToken ct = default);
}

public interface IProductGroupService
{
    Task<IReadOnlyList<ProductGroupDto>> ListAsync(CancellationToken ct = default);
    Task<ProductGroupDto> CreateAsync(CreateProductGroupRequest request, CancellationToken ct = default);
}

public interface IUnitService
{
    Task<IReadOnlyList<UnitDto>> ListAsync(CancellationToken ct = default);
    Task<UnitDto> CreateAsync(CreateUnitRequest request, CancellationToken ct = default);
}

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> ListAsync(CancellationToken ct = default);
    Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken ct = default);
}
