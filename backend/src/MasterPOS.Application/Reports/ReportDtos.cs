namespace MasterPOS.Application.Reports;

public record PaymentModeBreakdownDto(string PaymentMode, decimal Amount);

public record SalesSummaryDto(
    DateOnly FromDate, DateOnly ToDate, int OrderCount,
    decimal SubTotal, decimal Discount, decimal Vat, decimal GrandTotal,
    IReadOnlyList<PaymentModeBreakdownDto> ByPaymentMode);

public record PurchaseSummaryDto(
    DateOnly FromDate, DateOnly ToDate,
    int InvoiceCount, decimal InvoiceTotal, int ReturnCount, decimal ReturnTotal, decimal NetPurchase);

public record VatSummaryDto(
    DateOnly FromDate, DateOnly ToDate, decimal SalesVatCollected, decimal PurchaseVatPaid, decimal NetVatPayable);

public record StockValuationRowDto(Guid ProductId, string ProductName, decimal Balance, decimal UnitCost, decimal Value);

public record StockValuationDto(decimal TotalValue, IReadOnlyList<StockValuationRowDto> Rows);

public record TrialBalanceRowDto(Guid AccountId, string AccountName, string AccountType, decimal Debit, decimal Credit);

public record TrialBalanceDto(DateOnly AsOfDate, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<TrialBalanceRowDto> Rows);
