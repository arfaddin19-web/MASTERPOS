namespace MasterPOS.Application.Reports;

/// <summary>Read-only aggregates over data other modules already own —
/// nothing here writes anything. Honest about its own limits: VAT/Trial
/// Balance only reflect what's actually been posted (Sales/Purchase don't
/// auto-post journal entries yet, so a Trial Balance today is only as
/// complete as the journal entries manually recorded against it).</summary>
public interface IReportService
{
    Task<SalesSummaryDto> GetSalesSummaryAsync(DateOnly fromDate, DateOnly toDate, Guid? branchId = null, CancellationToken ct = default);
    Task<PurchaseSummaryDto> GetPurchaseSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<VatSummaryDto> GetVatSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<StockValuationDto> GetStockValuationAsync(Guid? warehouseId = null, CancellationToken ct = default);
    Task<TrialBalanceDto> GetTrialBalanceAsync(DateOnly asOfDate, CancellationToken ct = default);
}
