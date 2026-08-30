namespace MasterPOS.Application.Masters;

public interface IPartyService
{
    Task<PartyDto> CreateAsync(UpsertPartyRequest request, CancellationToken ct = default);
    Task<PartyDto> UpdateAsync(Guid id, UpsertPartyRequest request, CancellationToken ct = default);
    Task<PartyDto> SetActiveAsync(Guid id, SetPartyActiveRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PartyDto> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>partyType filters to "Supplier"/"Customer"/"Both" exactly, or
    /// pass null for everyone — a "Both" record shows up either way you'd
    /// naturally look for it since the quick-add flows filter by intent.</summary>
    Task<IReadOnlyList<PartyDto>> ListAsync(string? partyType = null, bool activeOnly = false, CancellationToken ct = default);
}

/// <summary>DiningTable.Status is never edited directly here — Sales owns
/// that transition (Vacant → Occupied → PartiallyPaid → Vacant) as orders
/// open, get part-paid, and close. This service only manages the table's
/// own identity (number, floor, seats).</summary>
public interface IDiningTableService
{
    Task<DiningTableDto> CreateAsync(CreateDiningTableRequest request, CancellationToken ct = default);
    Task<DiningTableDto> UpdateAsync(Guid id, UpdateDiningTableRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<DiningTableDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DiningTableDto>> ListAsync(Guid? branchId = null, CancellationToken ct = default);
}
