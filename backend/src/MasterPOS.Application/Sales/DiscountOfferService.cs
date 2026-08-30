using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Sales;

public class DiscountOfferService : IDiscountOfferService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public DiscountOfferService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DiscountOfferDto> CreateAsync(UpsertDiscountOfferRequest request, CancellationToken ct = default)
    {
        var (type, value) = Validate(request);
        var offer = new DiscountOffer
        {
            CompanyId = _currentUser.CompanyId,
            Name = request.Name,
            DiscountType = type,
            Value = value,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
        };
        _db.DiscountOffers.Add(offer);
        await _db.SaveChangesAsync(ct);
        return ToDto(offer);
    }

    public async Task<DiscountOfferDto> UpdateAsync(Guid id, UpsertDiscountOfferRequest request, CancellationToken ct = default)
    {
        var (type, value) = Validate(request);
        var offer = await GetOwnedAsync(id, ct);
        offer.Name = request.Name;
        offer.DiscountType = type;
        offer.Value = value;
        offer.ValidFrom = request.ValidFrom;
        offer.ValidTo = request.ValidTo;
        await _db.SaveChangesAsync(ct);
        return ToDto(offer);
    }

    public async Task<DiscountOfferDto> SetActiveAsync(Guid id, SetDiscountOfferActiveRequest request, CancellationToken ct = default)
    {
        var offer = await GetOwnedAsync(id, ct);
        offer.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToDto(offer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var offer = await GetOwnedAsync(id, ct);
        offer.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DiscountOfferDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _db.DiscountOffers.Where(o => o.CompanyId == _currentUser.CompanyId && !o.IsDeleted);
        if (activeOnly) query = query.Where(o => o.IsActive);

        var offers = await query.OrderBy(o => o.Name).ToListAsync(ct);
        return offers.Select(ToDto).ToList();
    }

    private static (DiscountType Type, decimal Value) Validate(UpsertDiscountOfferRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Name is required.");
        if (!Enum.TryParse<DiscountType>(request.DiscountType, ignoreCase: true, out var type))
            throw new AppException($"Unknown discount type '{request.DiscountType}'.");
        if (request.Value <= 0)
            throw new AppException("Value must be greater than zero.");
        if (type == DiscountType.Percent && request.Value > 100)
            throw new AppException("A Percent discount can't exceed 100.");
        if (request.ValidFrom is { } from && request.ValidTo is { } to && to < from)
            throw new AppException("Valid To can't be before Valid From.");
        return (type, request.Value);
    }

    private async Task<DiscountOffer> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var offer = await _db.DiscountOffers.SingleOrDefaultAsync(
            o => o.Id == id && o.CompanyId == _currentUser.CompanyId && !o.IsDeleted, ct);
        return offer ?? throw new AppException("Discount offer not found.");
    }

    private static DiscountOfferDto ToDto(DiscountOffer o) => new(
        o.Id, o.Name, o.DiscountType.ToString(), o.Value, o.ValidFrom, o.ValidTo, o.IsActive);
}
