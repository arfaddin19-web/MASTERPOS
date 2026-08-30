using MasterPOS.Application.Common;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Masters;

/// <summary>
/// The four "+ quick-add" lookups from the Product form (Category, Group,
/// Unit, Warehouse) — small enough that one file per module, matching
/// Infrastructure's <c>MastersConfigurations.cs</c> grouping, reads better
/// than four near-empty files.
/// </summary>
public class ProductCategoryService : IProductCategoryService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ProductCategoryService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> ListAsync(CancellationToken ct = default)
        => await _db.ProductCategories
            .Where(c => c.CompanyId == _currentUser.CompanyId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryDto(c.Id, c.Name, c.ParentCategoryId))
            .ToListAsync(ct);

    public async Task<ProductCategoryDto> CreateAsync(CreateProductCategoryRequest request, CancellationToken ct = default)
    {
        var exists = await _db.ProductCategories.AnyAsync(
            c => c.CompanyId == _currentUser.CompanyId && !c.IsDeleted && c.Name == request.Name, ct);
        if (exists)
            throw new AppException($"A category named '{request.Name}' already exists.");

        var category = new ProductCategory
        {
            CompanyId = _currentUser.CompanyId,
            Name = request.Name,
            ParentCategoryId = request.ParentCategoryId,
        };
        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return new ProductCategoryDto(category.Id, category.Name, category.ParentCategoryId);
    }
}

public class ProductGroupService : IProductGroupService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ProductGroupService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProductGroupDto>> ListAsync(CancellationToken ct = default)
        => await _db.ProductGroups
            .Where(g => g.CompanyId == _currentUser.CompanyId && !g.IsDeleted)
            .OrderBy(g => g.Name)
            .Select(g => new ProductGroupDto(g.Id, g.Name))
            .ToListAsync(ct);

    public async Task<ProductGroupDto> CreateAsync(CreateProductGroupRequest request, CancellationToken ct = default)
    {
        var exists = await _db.ProductGroups.AnyAsync(
            g => g.CompanyId == _currentUser.CompanyId && !g.IsDeleted && g.Name == request.Name, ct);
        if (exists)
            throw new AppException($"A group named '{request.Name}' already exists.");

        var group = new ProductGroup { CompanyId = _currentUser.CompanyId, Name = request.Name };
        _db.ProductGroups.Add(group);
        await _db.SaveChangesAsync(ct);
        return new ProductGroupDto(group.Id, group.Name);
    }
}

public class UnitService : IUnitService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public UnitService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<UnitDto>> ListAsync(CancellationToken ct = default)
        => await _db.Units
            .Where(u => u.CompanyId == _currentUser.CompanyId && !u.IsDeleted)
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(u.Id, u.Name, u.ShortCode))
            .ToListAsync(ct);

    public async Task<UnitDto> CreateAsync(CreateUnitRequest request, CancellationToken ct = default)
    {
        var exists = await _db.Units.AnyAsync(
            u => u.CompanyId == _currentUser.CompanyId && !u.IsDeleted && u.Name == request.Name, ct);
        if (exists)
            throw new AppException($"A unit named '{request.Name}' already exists.");

        var unit = new UnitOfMeasure { CompanyId = _currentUser.CompanyId, Name = request.Name, ShortCode = request.ShortCode };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(ct);
        return new UnitDto(unit.Id, unit.Name, unit.ShortCode);
    }
}

public class WarehouseService : IWarehouseService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public WarehouseService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<WarehouseDto>> ListAsync(CancellationToken ct = default)
        => await _db.Warehouses
            .Where(w => w.CompanyId == _currentUser.CompanyId && !w.IsDeleted)
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseDto(w.Id, w.Name, w.BranchId, w.IsDefault))
            .ToListAsync(ct);

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken ct = default)
    {
        var branchExists = await _db.Branches.AnyAsync(
            b => b.Id == request.BranchId && b.CompanyId == _currentUser.CompanyId && !b.IsDeleted, ct);
        if (!branchExists)
            throw new AppException("The selected branch does not exist.");

        var warehouse = new Warehouse
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = request.BranchId,
            Name = request.Name,
            IsDefault = request.IsDefault,
        };
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.BranchId, warehouse.IsDefault);
    }
}
