using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Reports;

public class ReportService : IReportService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ReportService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(DateOnly fromDate, DateOnly toDate, Guid? branchId = null, CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = ToUtcRange(fromDate, toDate);
        var query = _db.Orders
            .Where(o => o.CompanyId == _currentUser.CompanyId && o.Status == OrderStatus.Paid
                && o.ClosedAtUtc >= fromUtc && o.ClosedAtUtc < toUtc);
        if (branchId is { } b) query = query.Where(o => o.BranchId == b);

        var orders = await query.ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToList();

        var byMode = await _db.OrderPayments
            .Where(p => orderIds.Contains(p.OrderId))
            .GroupBy(p => p.PaymentMode)
            .Select(g => new PaymentModeBreakdownDto(g.Key.ToString(), g.Sum(p => p.Amount)))
            .ToListAsync(ct);

        return new SalesSummaryDto(
            fromDate, toDate, orders.Count,
            orders.Sum(o => o.SubTotalAmount), orders.Sum(o => o.DiscountAmount),
            orders.Sum(o => o.VatAmount), orders.Sum(o => o.GrandTotalAmount), byMode);
    }

    public async Task<PurchaseSummaryDto> GetPurchaseSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var invoices = await _db.PurchaseInvoices
            .Where(i => i.CompanyId == _currentUser.CompanyId && i.Status == DocumentStatus.Posted
                && i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
            .ToListAsync(ct);
        var returns = await _db.PurchaseReturns
            .Where(r => r.CompanyId == _currentUser.CompanyId && r.Status == DocumentStatus.Posted
                && r.ReturnDate >= fromDate && r.ReturnDate <= toDate)
            .ToListAsync(ct);

        var invoiceTotal = invoices.Sum(i => i.GrandTotalAmount);
        var returnTotal = returns.Sum(r => r.GrandTotalAmount);
        return new PurchaseSummaryDto(fromDate, toDate, invoices.Count, invoiceTotal, returns.Count, returnTotal, invoiceTotal - returnTotal);
    }

    public async Task<VatSummaryDto> GetVatSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = ToUtcRange(fromDate, toDate);
        var salesVat = await _db.Orders
            .Where(o => o.CompanyId == _currentUser.CompanyId && o.Status == OrderStatus.Paid
                && o.ClosedAtUtc >= fromUtc && o.ClosedAtUtc < toUtc)
            .SumAsync(o => (decimal?)o.VatAmount, ct) ?? 0m;

        var purchaseVatIn = await _db.PurchaseInvoices
            .Where(i => i.CompanyId == _currentUser.CompanyId && i.Status == DocumentStatus.Posted
                && i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
            .SumAsync(i => (decimal?)i.VatAmount, ct) ?? 0m;
        var purchaseVatOut = await _db.PurchaseReturns
            .Where(r => r.CompanyId == _currentUser.CompanyId && r.Status == DocumentStatus.Posted
                && r.ReturnDate >= fromDate && r.ReturnDate <= toDate)
            .SumAsync(r => (decimal?)r.VatAmount, ct) ?? 0m;
        var purchaseVat = purchaseVatIn - purchaseVatOut;

        return new VatSummaryDto(fromDate, toDate, salesVat, purchaseVat, salesVat - purchaseVat);
    }

    public async Task<StockValuationDto> GetStockValuationAsync(Guid? warehouseId = null, CancellationToken ct = default)
    {
        var query = _db.StockLedgerEntries.Where(e => e.CompanyId == _currentUser.CompanyId);
        if (warehouseId is { } w) query = query.Where(e => e.WarehouseId == w);

        var balances = await query
            .GroupBy(e => e.ProductId)
            .Select(g => new { ProductId = g.Key, Balance = g.Sum(e => e.QuantityIn) - g.Sum(e => e.QuantityOut) })
            .Where(g => g.Balance != 0)
            .ToListAsync(ct);
        if (balances.Count == 0) return new StockValuationDto(0m, Array.Empty<StockValuationRowDto>());

        var productIds = balances.Select(b => b.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var rows = balances
            .Select(b =>
            {
                var product = products[b.ProductId];
                var value = Math.Round(b.Balance * product.PurchasePrice, 2);
                return new StockValuationRowDto(b.ProductId, product.Name, b.Balance, product.PurchasePrice, value);
            })
            .OrderByDescending(r => r.Value)
            .ToList();

        return new StockValuationDto(rows.Sum(r => r.Value), rows);
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        var accounts = await _db.ChartOfAccounts
            .Where(a => a.CompanyId == _currentUser.CompanyId && !a.IsDeleted)
            .ToListAsync(ct);

        var journalTotals = await _db.JournalEntryLines
            .Where(l => l.JournalEntry.CompanyId == _currentUser.CompanyId
                && l.JournalEntry.Status == DocumentStatus.Posted && l.JournalEntry.EntryDate <= asOfDate)
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(l => l.DebitAmount), Credit = g.Sum(l => l.CreditAmount) })
            .ToDictionaryAsync(g => g.AccountId, ct);

        var openingByAccount = await _db.OpeningBalances
            .Where(b => b.CompanyId == _currentUser.CompanyId && !b.IsDeleted && b.AccountId != null && b.AsOfDate <= asOfDate)
            .ToListAsync(ct);

        var rows = new List<TrialBalanceRowDto>();
        foreach (var account in accounts)
        {
            var (jDebit, jCredit) = journalTotals.TryGetValue(account.Id, out var jt) ? (jt.Debit, jt.Credit) : (0m, 0m);
            var openingDebit = openingByAccount.Where(b => b.AccountId == account.Id && b.BalanceType == BalanceType.Dr).Sum(b => b.Amount);
            var openingCredit = openingByAccount.Where(b => b.AccountId == account.Id && b.BalanceType == BalanceType.Cr).Sum(b => b.Amount);

            var net = (jDebit + openingDebit) - (jCredit + openingCredit);
            if (net == 0) continue;
            rows.Add(net > 0
                ? new TrialBalanceRowDto(account.Id, account.Name, account.AccountType.ToString(), net, 0m)
                : new TrialBalanceRowDto(account.Id, account.Name, account.AccountType.ToString(), 0m, -net));
        }

        rows = rows.OrderBy(r => r.AccountType).ThenBy(r => r.AccountName).ToList();
        return new TrialBalanceDto(asOfDate, rows.Sum(r => r.Debit), rows.Sum(r => r.Credit), rows);
    }

    private static (DateTime FromUtc, DateTime ToUtcExclusive) ToUtcRange(DateOnly fromDate, DateOnly toDate)
        => (fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
