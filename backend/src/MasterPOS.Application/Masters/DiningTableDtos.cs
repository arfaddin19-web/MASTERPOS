namespace MasterPOS.Application.Masters;

public record DiningTableDto(
    Guid Id, Guid BranchId, string BranchName, string TableNumber,
    string? FloorLabel, int Seats, string Status);

public record CreateDiningTableRequest(Guid BranchId, string TableNumber, string? FloorLabel, int Seats);

public record UpdateDiningTableRequest(string TableNumber, string? FloorLabel, int Seats);
