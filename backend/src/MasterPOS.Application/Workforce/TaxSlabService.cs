using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Workforce;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Workforce;

public class TaxSlabService : ITaxSlabService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public TaxSlabService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TaxSlabDto>> ListAsync(CancellationToken ct = default)
    {
        var slabs = await _db.TaxSlabs
            .Where(s => s.CompanyId == _currentUser.CompanyId && !s.IsDeleted)
            .OrderBy(s => s.MaritalStatus).ThenBy(s => s.LowerBound)
            .ToListAsync(ct);
        return slabs.Select(ToDto).ToList();
    }

    public async Task<TaxSlabDto> CreateAsync(UpsertTaxSlabRequest request, CancellationToken ct = default)
    {
        var (status, lower, upper, rate) = Validate(request);
        await EnsureNoOverlapAsync(status, lower, upper, excludeId: null, ct);

        var slab = new TaxSlab
        {
            CompanyId = _currentUser.CompanyId,
            MaritalStatus = status,
            LowerBound = lower,
            UpperBound = upper,
            RatePercent = rate,
        };
        _db.TaxSlabs.Add(slab);
        await _db.SaveChangesAsync(ct);
        return ToDto(slab);
    }

    public async Task<TaxSlabDto> UpdateAsync(Guid id, UpsertTaxSlabRequest request, CancellationToken ct = default)
    {
        var (status, lower, upper, rate) = Validate(request);
        await EnsureNoOverlapAsync(status, lower, upper, excludeId: id, ct);

        var slab = await GetOwnedAsync(id, ct);
        slab.MaritalStatus = status;
        slab.LowerBound = lower;
        slab.UpperBound = upper;
        slab.RatePercent = rate;
        await _db.SaveChangesAsync(ct);
        return ToDto(slab);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var slab = await GetOwnedAsync(id, ct);
        slab.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TaxSlabDto>> SeedDefaultsAsync(CancellationToken ct = default)
    {
        var hasAny = await _db.TaxSlabs.AnyAsync(s => s.CompanyId == _currentUser.CompanyId && !s.IsDeleted, ct);
        if (hasAny)
            throw new AppException("Tax slabs are already configured — delete them first if you want to reseed the defaults.");

        // A commonly-cited recent Nepal individual income-tax slab structure —
        // a starting point for the admin to check against the current fiscal
        // year's official rates before relying on it, not a guarantee of them.
        // The government revises thresholds/rates most years.
        var defaults = new (MaritalStatus Status, decimal Lower, decimal? Upper, decimal Rate)[]
        {
            (MaritalStatus.Single, 0m, 500_000m, 1m),
            (MaritalStatus.Single, 500_000m, 700_000m, 10m),
            (MaritalStatus.Single, 700_000m, 1_000_000m, 20m),
            (MaritalStatus.Single, 1_000_000m, 2_000_000m, 30m),
            (MaritalStatus.Single, 2_000_000m, null, 36m),
            (MaritalStatus.Couple, 0m, 600_000m, 1m),
            (MaritalStatus.Couple, 600_000m, 800_000m, 10m),
            (MaritalStatus.Couple, 800_000m, 1_100_000m, 20m),
            (MaritalStatus.Couple, 1_100_000m, 2_000_000m, 30m),
            (MaritalStatus.Couple, 2_000_000m, null, 36m),
        };

        foreach (var (status, lower, upper, rate) in defaults)
        {
            _db.TaxSlabs.Add(new TaxSlab
            {
                CompanyId = _currentUser.CompanyId,
                MaritalStatus = status,
                LowerBound = lower,
                UpperBound = upper,
                RatePercent = rate,
            });
        }
        await _db.SaveChangesAsync(ct);

        return await ListAsync(ct);
    }

    private static (MaritalStatus Status, decimal Lower, decimal? Upper, decimal Rate) Validate(UpsertTaxSlabRequest request)
    {
        if (!Enum.TryParse<MaritalStatus>(request.MaritalStatus, ignoreCase: true, out var status))
            throw new AppException($"Unknown marital status '{request.MaritalStatus}'.");
        if (request.LowerBound < 0)
            throw new AppException("Lower bound can't be negative.");
        if (request.UpperBound is { } upper && upper <= request.LowerBound)
            throw new AppException("Upper bound must be greater than the lower bound.");
        if (request.RatePercent is < 0 or > 100)
            throw new AppException("Rate % must be between 0 and 100.");
        return (status, request.LowerBound, request.UpperBound, request.RatePercent);
    }

    /// <summary>Rejects a slab whose [Lower, Upper) range overlaps another
    /// slab already configured for the same marital status — a bad config
    /// here would silently mis-tax every employee under it.</summary>
    private async Task EnsureNoOverlapAsync(MaritalStatus status, decimal lower, decimal? upper, Guid? excludeId, CancellationToken ct)
    {
        var others = await _db.TaxSlabs
            .Where(s => s.CompanyId == _currentUser.CompanyId && s.MaritalStatus == status && !s.IsDeleted
                && (excludeId == null || s.Id != excludeId))
            .ToListAsync(ct);

        var newUpper = upper ?? decimal.MaxValue;
        foreach (var other in others)
        {
            var otherUpper = other.UpperBound ?? decimal.MaxValue;
            var overlaps = lower < otherUpper && other.LowerBound < newUpper;
            if (overlaps)
                throw new AppException(
                    $"This range overlaps the existing {other.MaritalStatus} slab " +
                    $"{other.LowerBound:0} – {(other.UpperBound?.ToString("0") ?? "∞")}.");
        }
    }

    private async Task<TaxSlab> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var slab = await _db.TaxSlabs.SingleOrDefaultAsync(
            s => s.Id == id && s.CompanyId == _currentUser.CompanyId && !s.IsDeleted, ct);
        return slab ?? throw new AppException("Tax slab not found.");
    }

    private static TaxSlabDto ToDto(TaxSlab s) => new(s.Id, s.MaritalStatus.ToString(), s.LowerBound, s.UpperBound, s.RatePercent);
}
