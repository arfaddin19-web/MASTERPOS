import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { AppShell } from '../components/AppShell';
import { getReorderSuggestions, getSalesSummary, getStockValuation } from '../api/reports';
import { useAuth } from '../auth/AuthContext';
import { todayIso } from '../lib/format';

// Dashboard KPI cards round to whole rupees (unlike the 2-decimal formatRs
// used everywhere money needs to reconcile exactly, e.g. POS/Transactions).
function formatRs(n: number) {
  return `Rs. ${n.toLocaleString('en-IN', { maximumFractionDigits: 0 })}`;
}

function greeting() {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 17) return 'Good afternoon';
  return 'Good evening';
}

export function DashboardPage() {
  const { session } = useAuth();
  const navigate = useNavigate();
  const today = todayIso();

  const salesQuery = useQuery({ queryKey: ['sales-summary', today], queryFn: () => getSalesSummary(today, today) });
  const stockQuery = useQuery({ queryKey: ['stock-valuation'], queryFn: () => getStockValuation() });
  const reorderQuery = useQuery({ queryKey: ['reorder-suggestions'], queryFn: getReorderSuggestions });

  return (
    <AppShell
      title="Dashboard"
      subtitle="Overview across all modules"
      topbarExtra={<div className="chip">{new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}</div>}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div>
          <div className="greet">
            {greeting()}, {session?.fullName}
          </div>
          <div className="page-sub" style={{ marginTop: 6 }}>
            Here's what's happening today.
          </div>
        </div>
        <button className="btn btn-primary" onClick={() => navigate('/pos')}>
          + New Sale
        </button>
      </div>

      <div className="kpi-grid">
        <div className="card">
          <div className="kpi-label">Today's Sales</div>
          <div className="kpi-num tabular">
            {salesQuery.isLoading ? <span className="spinner" /> : formatRs(salesQuery.data?.grandTotal ?? 0)}
          </div>
        </div>
        <div className="card">
          <div className="kpi-label">Orders Today</div>
          <div className="kpi-num tabular">{salesQuery.isLoading ? <span className="spinner" /> : salesQuery.data?.orderCount ?? 0}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Stock Value</div>
          <div className="kpi-num tabular">
            {stockQuery.isLoading ? <span className="spinner" /> : formatRs(stockQuery.data?.totalValue ?? 0)}
          </div>
        </div>
        <div className="card">
          <div className="kpi-label">Low Stock Items</div>
          <div className="kpi-num tabular">{reorderQuery.isLoading ? <span className="spinner" /> : reorderQuery.data?.length ?? 0}</div>
          <div className="kpi-foot">
            {(reorderQuery.data?.length ?? 0) > 0 ? (
              <span className="badge badge-danger">Needs attention</span>
            ) : (
              <span className="badge badge-success">On track</span>
            )}
          </div>
        </div>
      </div>

      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Sales by Payment Mode — Today</div>
          </div>
          {salesQuery.data && salesQuery.data.byPaymentMode.length > 0 ? (
            <div className="stack">
              {salesQuery.data.byPaymentMode.map((row) => (
                <div className="alert-row" key={row.paymentMode}>
                  <span>{row.paymentMode}</span>
                  <span className="tabular">{formatRs(row.amount)}</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="empty-state">No sales recorded yet today.</div>
          )}
        </div>

        <div className="card">
          <div className="card-head">
            <div className="card-title">Reorder Suggestions</div>
          </div>
          {reorderQuery.data && reorderQuery.data.length > 0 ? (
            <div className="stack">
              {reorderQuery.data.slice(0, 6).map((row) => (
                <div className="alert-row" key={row.productId}>
                  <span>{row.productName}</span>
                  <span className="badge badge-danger">short {row.shortBy}</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="empty-state">Everything's above its reorder level.</div>
          )}
        </div>
      </div>
    </AppShell>
  );
}
