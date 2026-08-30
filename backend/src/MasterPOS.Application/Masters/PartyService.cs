using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Masters;

public class PartyService : IPartyService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public PartyService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<PartyDto> CreateAsync(UpsertPartyRequest request, CancellationToken ct = default)
    {
        var (partyType, balanceType) = Validate(request);

        var party = new Party
        {
            CompanyId = _currentUser.CompanyId,
            PartyType = partyType,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            VatOrPanNumber = request.VatOrPanNumber,
            OpeningBalanceAmount = request.OpeningBalanceAmount,
            OpeningBalanceType = balanceType,
        };
        _db.Parties.Add(party);
        await _db.SaveChangesAsync(ct);
        return ToDto(party);
    }

    public async Task<PartyDto> UpdateAsync(Guid id, UpsertPartyRequest request, CancellationToken ct = default)
    {
        var (partyType, balanceType) = Validate(request);
        var party = await GetOwnedAsync(id, ct);

        if (await HasTransactionsAsync(id, ct))
            throw new AppException($"'{party.Name}' has transaction history and can no longer be edited — deactivate instead.");

        party.PartyType = partyType;
        party.Name = request.Name;
        party.Phone = request.Phone;
        party.Email = request.Email;
        party.Address = request.Address;
        party.VatOrPanNumber = request.VatOrPanNumber;
        party.OpeningBalanceAmount = request.OpeningBalanceAmount;
        party.OpeningBalanceType = balanceType;
        await _db.SaveChangesAsync(ct);
        return ToDto(party);
    }

    public async Task<PartyDto> SetActiveAsync(Guid id, SetPartyActiveRequest request, CancellationToken ct = default)
    {
        var party = await GetOwnedAsync(id, ct);
        party.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToDto(party);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var party = await GetOwnedAsync(id, ct);
        if (await HasTransactionsAsync(id, ct))
            throw new AppException($"'{party.Name}' has transaction history and can't be deleted — deactivate instead.");

        party.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Deleted", "Masters.Parties", party.Id, $"deleted party '{party.Name}'", ct);
    }

    public async Task<PartyDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<PartyDto>> ListAsync(string? partyType = null, bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _db.Parties.Where(p => p.CompanyId == _currentUser.CompanyId && !p.IsDeleted);
        if (activeOnly) query = query.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(partyType))
        {
            if (!Enum.TryParse<PartyType>(partyType, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown party type '{partyType}'.");
            // "Both" satisfies a filter for either Supplier or Customer, not just an exact match.
            query = query.Where(p => p.PartyType == parsed || p.PartyType == PartyType.Both);
        }

        var parties = await query.OrderBy(p => p.Name).ToListAsync(ct);
        return parties.Select(ToDto).ToList();
    }

    private async Task<bool> HasTransactionsAsync(Guid partyId, CancellationToken ct)
        => await _db.PurchaseInvoices.AnyAsync(i => i.SupplierId == partyId, ct)
        || await _db.PurchaseReturns.AnyAsync(r => r.SupplierId == partyId, ct)
        || await _db.Orders.AnyAsync(o => o.CustomerId == partyId, ct);

    private static (PartyType Type, BalanceType Balance) Validate(UpsertPartyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Name is required.");
        if (!Enum.TryParse<PartyType>(request.PartyType, ignoreCase: true, out var partyType))
            throw new AppException($"Unknown party type '{request.PartyType}'.");
        if (!Enum.TryParse<BalanceType>(request.OpeningBalanceType, ignoreCase: true, out var balanceType))
            throw new AppException($"Unknown balance type '{request.OpeningBalanceType}'.");
        if (request.OpeningBalanceAmount < 0)
            throw new AppException("Opening balance can't be negative — use the Dr/Cr type to indicate direction.");
        return (partyType, balanceType);
    }

    private async Task<Party> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var party = await _db.Parties.SingleOrDefaultAsync(
            p => p.Id == id && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct);
        return party ?? throw new AppException("Party not found.");
    }

    private static PartyDto ToDto(Party p) => new(
        p.Id, p.PartyType.ToString(), p.Name, p.Phone, p.Email, p.Address, p.VatOrPanNumber,
        p.OpeningBalanceAmount, p.OpeningBalanceType.ToString(), p.LoyaltyPoints, p.IsActive);
}
