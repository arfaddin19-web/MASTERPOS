using MasterPOS.Domain.Common;

namespace MasterPOS.Application.Masters;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> ListAsync(string? search = null, Guid? categoryId = null, ProductType? productType = null, CancellationToken ct = default);
    Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(UpsertProductRequest request, CancellationToken ct = default);

    /// <summary>Full field edit — rejected once the product has any transaction history
    /// (an Order/Purchase line, a stock movement, ...). Use <see cref="SetActiveAsync"/> to
    /// retire a transacted product instead; that's always allowed.</summary>
    Task<ProductDto> UpdateAsync(Guid id, UpsertProductRequest request, CancellationToken ct = default);

    /// <summary>Toggles IsActive only — the one edit still allowed on a product with
    /// transaction history, since it changes nothing a past transaction depended on.</summary>
    Task<ProductDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    /// <summary>Soft-deletes the product (sets IsDeleted) — rejected if the product has any
    /// transaction history (deactivate it instead) or is still used as a recipe ingredient.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>The Recipe's ingredient list — throws <see cref="Common.AppException"/> if the product isn't a Recipe.</summary>
    Task<IReadOnlyList<ProductBomLineDto>> GetBomAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Replaces the Recipe's whole ingredient list in one call — the Masters screen's "Save Changes" saves the BOM card as a unit, not line by line.</summary>
    Task<IReadOnlyList<ProductBomLineDto>> SetBomAsync(Guid productId, SetProductBomRequest request, CancellationToken ct = default);
}
