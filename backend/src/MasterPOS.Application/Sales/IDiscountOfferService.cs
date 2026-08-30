namespace MasterPOS.Application.Sales;

public interface IDiscountOfferService
{
    Task<DiscountOfferDto> CreateAsync(UpsertDiscountOfferRequest request, CancellationToken ct = default);
    Task<DiscountOfferDto> UpdateAsync(Guid id, UpsertDiscountOfferRequest request, CancellationToken ct = default);
    Task<DiscountOfferDto> SetActiveAsync(Guid id, SetDiscountOfferActiveRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DiscountOfferDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default);
}
