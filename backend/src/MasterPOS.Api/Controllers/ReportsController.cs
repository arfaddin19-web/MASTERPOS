using MasterPOS.Application.Common;
using MasterPOS.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet("sales-summary")]
    public async Task<ActionResult<SalesSummaryDto>> SalesSummary(
        [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, [FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _reports.GetSalesSummaryAsync(fromDate, toDate, branchId, ct));

    [HttpGet("purchase-summary")]
    public async Task<ActionResult<PurchaseSummaryDto>> PurchaseSummary(
        [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken ct)
        => Ok(await _reports.GetPurchaseSummaryAsync(fromDate, toDate, ct));

    [HttpGet("vat-summary")]
    public async Task<ActionResult<VatSummaryDto>> VatSummary(
        [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken ct)
        => Ok(await _reports.GetVatSummaryAsync(fromDate, toDate, ct));

    [HttpGet("stock-valuation")]
    public async Task<ActionResult<StockValuationDto>> StockValuation([FromQuery] Guid? warehouseId, CancellationToken ct)
        => Ok(await _reports.GetStockValuationAsync(warehouseId, ct));

    [HttpGet("trial-balance")]
    public async Task<ActionResult<TrialBalanceDto>> TrialBalance([FromQuery] DateOnly asOfDate, CancellationToken ct)
        => Ok(await _reports.GetTrialBalanceAsync(asOfDate, ct));
}
