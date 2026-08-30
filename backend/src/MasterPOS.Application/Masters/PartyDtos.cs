namespace MasterPOS.Application.Masters;

public record PartyDto(
    Guid Id, string PartyType, string Name, string? Phone, string? Email, string? Address,
    string? VatOrPanNumber, decimal OpeningBalanceAmount, string OpeningBalanceType,
    int LoyaltyPoints, bool IsActive);

public record UpsertPartyRequest(
    string PartyType, string Name, string? Phone, string? Email, string? Address,
    string? VatOrPanNumber, decimal OpeningBalanceAmount, string OpeningBalanceType);

public record SetPartyActiveRequest(bool IsActive);
