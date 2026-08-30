using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Masters;

public class ProductService : IProductService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public ProductService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(
        string? search = null, Guid? categoryId = null, ProductType? productType = null, CancellationToken ct = default)
    {
        var query = _db.Products
            .Include(p => p.Category).Include(p => p.Group).Include(p => p.Unit).Include(p => p.DefaultWarehouse)
            .Where(p => p.CompanyId == _currentUser.CompanyId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)));
        if (categoryId is { } catId)
            query = query.Where(p => p.CategoryId == catId);
        if (productType is { } type)
            query = query.Where(p => p.ProductType == type);

        var products = await query.OrderBy(p => p.Name).ToListAsync(ct);
        return products.Select(ToDto).ToList();
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await FindOwnedAsync(id, ct));

    public async Task<ProductDto> CreateAsync(UpsertProductRequest request, CancellationToken ct = default)
    {
        var (productType, kotStation) = ParseTypes(request);
        await ValidateReferencesAsync(request, ct);
        await ValidateBarcodeUniqueAsync(request.Barcode, existingProductId: null, ct);

        var product = new Product { CompanyId = _currentUser.CompanyId };
        Apply(product, request, productType, kotStation);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return ToDto(await FindOwnedAsync(product.Id, ct));
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpsertProductRequest request, CancellationToken ct = default)
    {
        var product = await FindOwnedAsync(id, ct);
        if (await HasTransactionsAsync(id, ct))
            throw new AppException(
                $"'{product.Name}' has transaction history and can no longer be edited — " +
                "use Deactivate instead, or create a new product for any change.");

        var (productType, kotStation) = ParseTypes(request);
        await ValidateReferencesAsync(request, ct);
        await ValidateBarcodeUniqueAsync(request.Barcode, existingProductId: id, ct);

        Apply(product, request, productType, kotStation);
        await _db.SaveChangesAsync(ct);

        return ToDto(await FindOwnedAsync(id, ct));
    }

    public async Task<ProductDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var product = await FindOwnedAsync(id, ct);
        product.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await FindOwnedAsync(id, ct);

        if (await HasTransactionsAsync(id, ct))
            throw new AppException($"'{product.Name}' has transaction history and can't be deleted — deactivate it instead.");

        var usedInRecipes = await _db.ProductBoms
            .Where(b => b.ComponentProductId == id && !b.IsDeleted)
            .Include(b => b.FinishedProduct)
            .Select(b => b.FinishedProduct.Name)
            .Distinct()
            .ToListAsync(ct);
        if (usedInRecipes.Count > 0)
            throw new AppException($"'{product.Name}' is used as an ingredient in {string.Join(", ", usedInRecipes)} — remove it from those recipes first.");

        product.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Deleted", "Masters.Products", product.Id, $"deleted product '{product.Name}'", ct);
    }

    /// <summary>
    /// True once the product appears in any real transaction — a sale, a purchase, or a
    /// stock movement of any kind. Once true, the product's fields are frozen (see
    /// <see cref="UpdateAsync"/>) so past documents keep meaning exactly what they said at
    /// the time; <see cref="SetActiveAsync"/> is the only edit still allowed.
    /// </summary>
    private async Task<bool> HasTransactionsAsync(Guid productId, CancellationToken ct)
        => await _db.OrderLines.AnyAsync(l => l.ProductId == productId, ct)
        || await _db.PurchaseInvoiceLines.AnyAsync(l => l.ProductId == productId, ct)
        || await _db.PurchaseReturnLines.AnyAsync(l => l.ProductId == productId, ct)
        || await _db.StockLedgerEntries.AnyAsync(l => l.ProductId == productId, ct)
        || await _db.StockAdjustments.AnyAsync(l => l.ProductId == productId, ct)
        || await _db.StockTransfers.AnyAsync(l => l.ProductId == productId, ct)
        || await _db.OpeningStocks.AnyAsync(l => l.ProductId == productId, ct);

    public async Task<IReadOnlyList<ProductBomLineDto>> GetBomAsync(Guid productId, CancellationToken ct = default)
    {
        await GetRecipeProductAsync(productId, ct);

        return await _db.ProductBoms
            .Include(b => b.ComponentProduct).ThenInclude(p => p.Unit)
            .Where(b => b.FinishedProductId == productId && !b.IsDeleted)
            .OrderBy(b => b.ComponentProduct.Name)
            .Select(b => new ProductBomLineDto(b.ComponentProductId, b.ComponentProduct.Name, b.ComponentProduct.Unit.Name, b.Quantity))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductBomLineDto>> SetBomAsync(Guid productId, SetProductBomRequest request, CancellationToken ct = default)
    {
        await GetRecipeProductAsync(productId, ct);

        if (request.Lines.Count == 0)
            throw new AppException("A recipe needs at least one ingredient.");
        if (request.Lines.Any(l => l.ComponentProductId == productId))
            throw new AppException("A recipe cannot use itself as an ingredient.");
        if (request.Lines.Select(l => l.ComponentProductId).Distinct().Count() != request.Lines.Count)
            throw new AppException("Each ingredient can only appear once in the recipe.");

        // Ingredients must be stocked Inventory items — never another Recipe (no sub-recipes),
        // a non-stock Service, or an internal-use-only Consumable. See Product's class remarks.
        var componentIds = request.Lines.Select(l => l.ComponentProductId).ToList();
        var components = await _db.Products
            .Where(p => p.CompanyId == _currentUser.CompanyId && !p.IsDeleted && componentIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var line in request.Lines)
        {
            if (!components.TryGetValue(line.ComponentProductId, out var component))
                throw new AppException("One of the selected ingredients no longer exists.");
            if (component.ProductType != ProductType.Inventory)
                throw new AppException($"'{component.Name}' can't be used as an ingredient — only Inventory-type products can be recipe components.");
            if (line.Quantity <= 0)
                throw new AppException($"Quantity for '{component.Name}' must be greater than zero.");
        }

        var existingLines = await _db.ProductBoms.Where(b => b.FinishedProductId == productId).ToListAsync(ct);
        _db.ProductBoms.RemoveRange(existingLines);
        foreach (var line in request.Lines)
        {
            _db.ProductBoms.Add(new ProductBom
            {
                FinishedProductId = productId,
                ComponentProductId = line.ComponentProductId,
                Quantity = line.Quantity,
            });
        }
        await _db.SaveChangesAsync(ct);

        return await GetBomAsync(productId, ct);
    }

    private async Task<Product> GetRecipeProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await FindOwnedAsync(productId, ct);
        if (product.ProductType != ProductType.Recipe)
            throw new AppException($"'{product.Name}' isn't a Recipe product — only Recipe items have a BOM.");
        return product;
    }

    private async Task<Product> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var product = await _db.Products
            .Include(p => p.Category).Include(p => p.Group).Include(p => p.Unit).Include(p => p.DefaultWarehouse)
            .SingleOrDefaultAsync(p => p.Id == id && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct);
        return product ?? throw new AppException("Product not found.");
    }

    private static (ProductType, KotStation?) ParseTypes(UpsertProductRequest request)
    {
        if (!Enum.TryParse<ProductType>(request.ProductType, ignoreCase: true, out var productType))
            throw new AppException($"Unknown product type '{request.ProductType}'.");

        KotStation? kotStation = null;
        if (!string.IsNullOrWhiteSpace(request.KotStation))
        {
            if (!Enum.TryParse<KotStation>(request.KotStation, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown KOT station '{request.KotStation}'.");
            kotStation = parsed;
        }
        return (productType, kotStation);
    }

    private async Task ValidateReferencesAsync(UpsertProductRequest request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId;

        if (!await _db.Units.AnyAsync(u => u.Id == request.UnitId && u.CompanyId == companyId && !u.IsDeleted, ct))
            throw new AppException("The selected unit does not exist.");

        if (request.CategoryId is { } categoryId
            && !await _db.ProductCategories.AnyAsync(c => c.Id == categoryId && c.CompanyId == companyId && !c.IsDeleted, ct))
            throw new AppException("The selected category does not exist.");

        if (request.GroupId is { } groupId
            && !await _db.ProductGroups.AnyAsync(g => g.Id == groupId && g.CompanyId == companyId && !g.IsDeleted, ct))
            throw new AppException("The selected group does not exist.");

        if (request.DefaultWarehouseId is { } warehouseId
            && !await _db.Warehouses.AnyAsync(w => w.Id == warehouseId && w.CompanyId == companyId && !w.IsDeleted, ct))
            throw new AppException("The selected warehouse does not exist.");
    }

    private async Task ValidateBarcodeUniqueAsync(string? barcode, Guid? existingProductId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        var companyId = _currentUser.CompanyId;
        var inUse = await _db.Products.AnyAsync(
            p => p.CompanyId == companyId && !p.IsDeleted && p.Barcode == barcode && p.Id != existingProductId, ct);
        if (inUse)
            throw new AppException($"Barcode '{barcode}' is already used by another product.");
    }

    private static void Apply(Product product, UpsertProductRequest request, ProductType productType, KotStation? kotStation)
    {
        product.Name = request.Name;
        product.ProductType = productType;
        product.CategoryId = request.CategoryId;
        product.GroupId = request.GroupId;
        product.UnitId = request.UnitId;
        product.DefaultWarehouseId = request.DefaultWarehouseId;
        product.Barcode = request.Barcode;
        product.PurchasePrice = request.PurchasePrice;
        product.SalePrice = request.SalePrice;
        product.IsVatApplicable = request.IsVatApplicable;
        product.ReorderLevel = request.ReorderLevel;
        product.KotStation = kotStation;
        product.PrepTimeMinutes = request.PrepTimeMinutes;
        product.TrackInPos = request.TrackInPos;
        product.IsActive = request.IsActive;
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.ProductType.ToString(),
        p.CategoryId, p.Category?.Name,
        p.GroupId, p.Group?.Name,
        p.UnitId, p.Unit.Name,
        p.DefaultWarehouseId, p.DefaultWarehouse?.Name,
        p.Barcode, p.PurchasePrice, p.SalePrice, p.IsVatApplicable, p.ReorderLevel,
        p.KotStation?.ToString(), p.PrepTimeMinutes, p.TrackInPos, p.IsActive);
}
