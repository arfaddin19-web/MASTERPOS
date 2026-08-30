import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AppShell } from '../components/AppShell';
import { getPurchaseSummary, getSalesSummary, getStockValuation, getTrialBalance, getVatSummary } from '../api/reports';
import { formatDate, formatRs, todayIso } from '../lib/format';

const REPORTS = [
  { group: 'Sales', name: 'Sales Summary' },
  { group: 'Purchase', name: 'Purchase Summary' },
  { group: 'Tax', name: 'VAT Summary' },
  { group: 'Inventory', name: 'Stock Valuation' },
  { group: 'Final Accounts', name: 'Trial Balance' },
] as const;
type ReportName = (typeof REPORTS)[number]['name'];
const GROUPS = [...new Set(REPORTS.map((r) => r.group))];

function firstOfMonthIso() {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10);
}

export function ReportsPage() {
  const [active, setActive] = useState<ReportName>('Sales Summary');
  const [fromDate, setFromDate] = useState(firstOfMonthIso());
  const [toDate, setToDate] = useState(todayIso());
  const [asOfDate, setAsOfDate] = useState(todayIso());

  const salesQuery = useQuery({ queryKey: ['report-sales-summary', fromDate, toDate], queryFn: () => getSalesSummary(fromDate, toDate), enabled: active === 'Sales Summary' });
  const purchaseQuery = useQuery({ queryKey: ['report-purchase-summary', fromDate, toDate], queryFn: () => getPurchaseSummary(fromDate, toDate), enabled: active === 'Purchase Summary' });
  const vatQuery = useQuery({ queryKey: ['report-vat-summary', fromDate, toDate], queryFn: () => getVatSummary(fromDate, toDate), enabled: active === 'VAT Summary' });
  const stockQuery = useQuery({ queryKey: ['report-stock-valuation'], queryFn: () => getStockValuation(), enabled: active === 'Stock Valuation' });
  const trialQuery = useQuery({ queryKey: ['report-trial-balance', asOfDate], queryFn: () => getTrialBalance(asOfDate), enabled: active === 'Trial Balance' });

  const needsDateRange = active !== 'Stock Valuation' && active !== 'Trial Balance';

  return (
    <AppShell title="Reports" subtitle="Sales, purchase, VAT & financial statements">
      <div style={{ flex: 1, display: 'flex', gap: 18, minHeight: 0 }}>
        <div className="rcats">
          {GROUPS.map((g) => (
            <div key={g}>
              <div className="rgroup-label">{g}</div>
              {REPORTS.filter((r) => r.group === g).map((r) => (
                <button key={r.name} className={`ritem${active === r.name ? ' on' : ''}`} onClick={() => setActive(r.name)}>
                  {r.name}
                </button>
              ))}
            </div>
          ))}
        </div>

        <div className="rmain">
          <div className="rmain-head">
            <div>
              <div className="rtitle">{active}</div>
              <div className="rsub">
                {needsDateRange ? `${formatDate(fromDate)} – ${formatDate(toDate)}` : active === 'Trial Balance' ? `As of ${formatDate(asOfDate)}` : 'As of now'}
              </div>
            </div>
            <div className="rfilters">
              {needsDateRange && (
                <>
                  <input className="input" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} style={{ width: 150 }} />
                  <input className="input" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} style={{ width: 150 }} />
                </>
              )}
              {active === 'Trial Balance' && <input className="input" type="date" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} style={{ width: 150 }} />}
            </div>
          </div>

          {active === 'Sales Summary' && (
            <>
              {salesQuery.isLoading ? (
                <div className="empty-state">
                  <span className="spinner" />
                </div>
              ) : (
                <>
                  <div className="pl-section">
                    <div className="pl-label">Overview</div>
                    <div className="pl-row">
                      <span>Orders</span>
                      <span className="tabular">{salesQuery.data?.orderCount ?? 0}</span>
                    </div>
                    <div className="pl-row">
                      <span>Subtotal</span>
                      <span className="tabular">{formatRs(salesQuery.data?.subTotal)}</span>
                    </div>
                    <div className="pl-row">
                      <span>Discount</span>
                      <span className="tabular">– {formatRs(salesQuery.data?.discount)}</span>
                    </div>
                    <div className="pl-row">
                      <span>VAT</span>
                      <span className="tabular">{formatRs(salesQuery.data?.vat)}</span>
                    </div>
                    <div className="pl-row net">
                      <span>Grand Total</span>
                      <span className="tabular">{formatRs(salesQuery.data?.grandTotal)}</span>
                    </div>
                  </div>
                  <div className="pl-section">
                    <div className="pl-label">By Payment Mode</div>
                    {(salesQuery.data?.byPaymentMode ?? []).map((row) => (
                      <div className="pl-row" key={row.paymentMode}>
                        <span>{row.paymentMode}</span>
                        <span className="tabular">{formatRs(row.amount)}</span>
                      </div>
                    ))}
                    {(salesQuery.data?.byPaymentMode.length ?? 0) === 0 && <div className="page-sub">No paid orders in this range.</div>}
                  </div>
                </>
              )}
            </>
          )}

          {active === 'Purchase Summary' && (
            <>
              {purchaseQuery.isLoading ? (
                <div className="empty-state">
                  <span className="spinner" />
                </div>
              ) : (
                <div className="pl-section">
                  <div className="pl-row">
                    <span>Invoices Posted</span>
                    <span className="tabular">{purchaseQuery.data?.invoiceCount ?? 0}</span>
                  </div>
                  <div className="pl-row">
                    <span>Invoice Total</span>
                    <span className="tabular">{formatRs(purchaseQuery.data?.invoiceTotal)}</span>
                  </div>
                  <div className="pl-row">
                    <span>Returns Posted</span>
                    <span className="tabular">{purchaseQuery.data?.returnCount ?? 0}</span>
                  </div>
                  <div className="pl-row">
                    <span>Return Total</span>
                    <span className="tabular">– {formatRs(purchaseQuery.data?.returnTotal)}</span>
                  </div>
                  <div className="pl-row net">
                    <span>Net Purchase</span>
                    <span className="tabular">{formatRs(purchaseQuery.data?.netPurchase)}</span>
                  </div>
                </div>
              )}
            </>
          )}

          {active === 'VAT Summary' && (
            <>
              {vatQuery.isLoading ? (
                <div className="empty-state">
                  <span className="spinner" />
                </div>
              ) : (
                <div className="pl-section">
                  <div className="pl-row">
                    <span>Sales VAT Collected</span>
                    <span className="tabular">{formatRs(vatQuery.data?.salesVatCollected)}</span>
                  </div>
                  <div className="pl-row">
                    <span>Purchase VAT Paid</span>
                    <span className="tabular">– {formatRs(vatQuery.data?.purchaseVatPaid)}</span>
                  </div>
                  <div className="pl-row net">
                    <span>Net VAT Payable</span>
                    <span className="tabular">{formatRs(vatQuery.data?.netVatPayable)}</span>
                  </div>
                </div>
              )}
            </>
          )}

          {active === 'Stock Valuation' && (
            <>
              {stockQuery.isLoading ? (
                <div className="empty-state">
                  <span className="spinner" />
                </div>
              ) : (
                <>
                  <div className="pl-row net" style={{ borderTop: 'none' }}>
                    <span>Total Value</span>
                    <span className="tabular">{formatRs(stockQuery.data?.totalValue)}</span>
                  </div>
                  <div className="scroll-x" style={{ marginTop: 16 }}>
                    <table>
                      <thead>
                        <tr>
                          <th>Item</th>
                          <th style={{ textAlign: 'right' }}>Balance</th>
                          <th style={{ textAlign: 'right' }}>Unit Cost</th>
                          <th style={{ textAlign: 'right' }}>Value</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(stockQuery.data?.rows ?? []).map((r) => (
                          <tr key={r.productId}>
                            <td style={{ color: 'var(--text)' }}>{r.productName}</td>
                            <td style={{ textAlign: 'right' }} className="tabular">
                              {r.balance}
                            </td>
                            <td style={{ textAlign: 'right' }} className="tabular">
                              {formatRs(r.unitCost)}
                            </td>
                            <td style={{ textAlign: 'right', color: 'var(--text)' }} className="tabular">
                              {formatRs(r.value)}
                            </td>
                          </tr>
                        ))}
                        {(stockQuery.data?.rows.length ?? 0) === 0 && (
                          <tr>
                            <td colSpan={4} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                              No stock on hand.
                            </td>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </>
          )}

          {active === 'Trial Balance' && (
            <>
              {trialQuery.isLoading ? (
                <div className="empty-state">
                  <span className="spinner" />
                </div>
              ) : (
                <>
                  <div className="scroll-x">
                    <table>
                      <thead>
                        <tr>
                          <th>Account</th>
                          <th>Type</th>
                          <th style={{ textAlign: 'right' }}>Debit</th>
                          <th style={{ textAlign: 'right' }}>Credit</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(trialQuery.data?.rows ?? []).map((r) => (
                          <tr key={r.accountId}>
                            <td style={{ color: 'var(--text)' }}>{r.accountName}</td>
                            <td>{r.accountType}</td>
                            <td style={{ textAlign: 'right' }} className="tabular">
                              {r.debit > 0 ? formatRs(r.debit) : '—'}
                            </td>
                            <td style={{ textAlign: 'right' }} className="tabular">
                              {r.credit > 0 ? formatRs(r.credit) : '—'}
                            </td>
                          </tr>
                        ))}
                        {(trialQuery.data?.rows.length ?? 0) === 0 && (
                          <tr>
                            <td colSpan={4} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                              Nothing posted yet.
                            </td>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>
                  <div className="pl-row net">
                    <span>Total</span>
                    <span className="tabular" style={{ color: trialQuery.data && trialQuery.data.totalDebit === trialQuery.data.totalCredit ? 'var(--success)' : 'var(--danger)' }}>
                      {formatRs(trialQuery.data?.totalDebit)} / {formatRs(trialQuery.data?.totalCredit)}
                    </span>
                  </div>
                </>
              )}
            </>
          )}
        </div>
      </div>
    </AppShell>
  );
}
