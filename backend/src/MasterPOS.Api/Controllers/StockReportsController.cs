using MasterPOS.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterPOS.Api.Controllers;

/// <summary>The Stock Register / Item Ledger and Reorder Suggestions screens
/// — read-only queries over StockLedgerEntries, nothing here writes to it.</summary>
[Authorize]
[ApiController]
[Route("api/inventory/reports")]
public class StockReportsController : ControllerBase
{
    private readonly IStockReportService _reports;

    public StockReportsController(IStockReportService reports) => _reports = reports;

    [HttpGet("ledger")]
    public async Task<ActionResult<IReadOnlyList<StockLedgerEntryDto>>> Ledger(
        [FromQuery] Guid? productId, [FromQuery] Guid? warehouseId,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
        => Ok(await _reports.GetLedgerAsync(productId, warehouseId, fromDate, toDate, ct));

    [HttpGet("balances")]
    public async Task<ActionResult<IReadOnlyList<StockBalanceDto>>> Balances([FromQuery] Guid? warehouseId, CancellationToken ct)
        => Ok(await _reports.GetBalancesAsync(warehouseId, ct));

    [HttpGet("reorder-suggestions")]
    public async Task<ActionResult<IReadOnlyList<ReorderSuggestionDto>>> ReorderSuggestions(CancellationToken ct)
        => Ok(await _reports.GetReorderSuggestionsAsync(ct));
}
