using MasterPOS.Application.Common;
using MasterPOS.Application.Masters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

/// <summary>
/// The "+ quick-add" lookups from the Product form. Grouped in one
/// controller — each is a two-endpoint list/create pair, not worth its own file.
/// </summary>
[Authorize]
[ApiController]
[Route("api/masters")]
public class MastersLookupsController : ControllerBase
{
    private readonly IProductCategoryService _categories;
    private readonly IProductGroupService _groups;
    private readonly IUnitService _units;
    private readonly IWarehouseService _warehouses;

    public MastersLookupsController(
        IProductCategoryService categories, IProductGroupService groups, IUnitService units, IWarehouseService warehouses)
    {
        _categories = categories;
        _groups = groups;
        _units = units;
        _warehouses = warehouses;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<ProductCategoryDto>>> ListCategories(CancellationToken ct)
        => Ok(await _categories.ListAsync(ct));

    [HttpPost("categories")]
    public async Task<ActionResult<ProductCategoryDto>> CreateCategory(CreateProductCategoryRequest request, CancellationToken ct)
    {
        try { return Ok(await _categories.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<ProductGroupDto>>> ListGroups(CancellationToken ct)
        => Ok(await _groups.ListAsync(ct));

    [HttpPost("groups")]
    public async Task<ActionResult<ProductGroupDto>> CreateGroup(CreateProductGroupRequest request, CancellationToken ct)
    {
        try { return Ok(await _groups.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("units")]
    public async Task<ActionResult<IReadOnlyList<UnitDto>>> ListUnits(CancellationToken ct)
        => Ok(await _units.ListAsync(ct));

    [HttpPost("units")]
    public async Task<ActionResult<UnitDto>> CreateUnit(CreateUnitRequest request, CancellationToken ct)
    {
        try { return Ok(await _units.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("warehouses")]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> ListWarehouses(CancellationToken ct)
        => Ok(await _warehouses.ListAsync(ct));

    [HttpPost("warehouses")]
    public async Task<ActionResult<WarehouseDto>> CreateWarehouse(CreateWarehouseRequest request, CancellationToken ct)
    {
        try { return Ok(await _warehouses.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
