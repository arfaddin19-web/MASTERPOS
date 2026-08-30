using MasterPOS.Application.Common;
using MasterPOS.Application.Masters;
using MasterPOS.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/masters/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List(
        [FromQuery] string? search, [FromQuery] Guid? categoryId, [FromQuery] string? productType, CancellationToken ct)
    {
        ProductType? type = null;
        if (!string.IsNullOrWhiteSpace(productType))
        {
            if (!Enum.TryParse<ProductType>(productType, ignoreCase: true, out var parsed))
                return BadRequest(new { message = $"Unknown product type '{productType}'." });
            type = parsed;
        }
        return Ok(await _products.ListAsync(search, categoryId, type, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _products.GetAsync(id, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(UpsertProductRequest request, CancellationToken ct)
    {
        try { return Ok(await _products.CreateAsync(request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpsertProductRequest request, CancellationToken ct)
    {
        // "not found" (404) vs. "has transaction history, edit refused" (409) vs. anything
        // else — bad type/reference/duplicate barcode (400).
        try { return Ok(await _products.UpdateAsync(id, request, ct)); }
        catch (AppException ex) when (ex.Message == "Product not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) when (ex.Message.Contains("transaction history")) { return Conflict(new { message = ex.Message }); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>The one edit still allowed once a product has transaction history — see <see cref="Update"/>.</summary>
    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<ProductDto>> SetActive(Guid id, SetProductActiveRequest request, CancellationToken ct)
    {
        try { return Ok(await _products.SetActiveAsync(id, request.IsActive, ct)); }
        catch (AppException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // AppException here covers three different cases with different status codes:
        // "not found" (404) vs. "can't delete, has transactions / still used as a recipe
        // ingredient" (409).
        try { await _products.DeleteAsync(id, ct); return NoContent(); }
        catch (AppException ex) when (ex.Message == "Product not found.") { return NotFound(new { message = ex.Message }); }
        catch (AppException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}/bom")]
    public async Task<ActionResult<IReadOnlyList<ProductBomLineDto>>> GetBom(Guid id, CancellationToken ct)
    {
        try { return Ok(await _products.GetBomAsync(id, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}/bom")]
    public async Task<ActionResult<IReadOnlyList<ProductBomLineDto>>> SetBom(Guid id, SetProductBomRequest request, CancellationToken ct)
    {
        try { return Ok(await _products.SetBomAsync(id, request, ct)); }
        catch (AppException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
