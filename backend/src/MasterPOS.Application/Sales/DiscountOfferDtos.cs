namespace MasterPOS.Application.Sales;

public record DiscountOfferDto(
    Guid Id, string Name, string DiscountType, decimal Value, DateOnly? ValidFrom, DateOnly? ValidTo, bool IsActive);

public record UpsertDiscountOfferRequest(string Name, string DiscountType, decimal Value, DateOnly? ValidFrom, DateOnly? ValidTo);

public record SetDiscountOfferActiveRequest(bool IsActive);
