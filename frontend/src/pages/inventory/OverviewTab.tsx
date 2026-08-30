import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listWarehouses } from '../../api/masters';
import { getBalances } from '../../api/inventory';
import { getReorderSuggestions, getStockValuation } from '../../api/reports';
import { formatRs } from '../../lib/format';

export function OverviewTab() {
  const [warehouseId, setWarehouseId] = useState<string>('');

  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: listWarehouses });
  const balancesQuery = useQuery({ queryKey: ['stock-balances', warehouseId], queryFn: () => getBalances(warehouseId || undefined) });
  const valuationQuery = useQuery({ queryKey: ['stock-valuation'], queryFn: () => getStockValuation() });
  const reorderQuery = useQuery({ queryKey: ['reorder-suggestions'], queryFn: getReorderSuggestions });

  const reorderByProduct = useMemo(() => {
    const map = new Map<string, number>();
    (reorderQuery.data ?? []).forEach((r) => map.set(r.productId, r.reorderLevel));
    return map;
  }, [reorderQuery.data]);

  const activeSkus = new Set((balancesQuery.data ?? []).map((b) => b.productId)).size;

  return (
    <>
      <div className="kpi-grid">
        <div className="card">
          <div className="kpi-label">Total Stock Value</div>
          <div className="kpi-num tabular">{valuationQuery.isLoading ? <span className="spinner" /> : formatRs(valuationQuery.data?.totalValue)}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Stocked SKUs</div>
          <div className="kpi-num tabular">{balancesQuery.isLoading ? <span className="spinner" /> : activeSkus}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Low Stock Items</div>
          <div className="kpi-num tabular" style={{ color: (reorderQuery.data?.length ?? 0) > 0 ? 'var(--danger)' : undefined }}>
            {reorderQuery.isLoading ? <span className="spinner" /> : reorderQuery.data?.length ?? 0}
          </div>
        </div>
        <div className="card">
          <div className="kpi-label">Warehouses</div>
          <div className="kpi-num tabular">{warehousesQuery.data?.length ?? 0}</div>
        </div>
      </div>

      <div className="tabstrip">
        <button className={`tabstrip-btn${warehouseId === '' ? ' on' : ''}`} onClick={() => setWarehouseId('')}>
          All Warehouses
        </button>
        {(warehousesQuery.data ?? []).map((w) => (
          <button key={w.id} className={`tabstrip-btn${warehouseId === w.id ? ' on' : ''}`} onClick={() => setWarehouseId(w.id)}>
            {w.name}
          </button>
        ))}
      </div>

      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Stock Balances</div>
            <span className="chip">{(balancesQuery.data ?? []).length} rows</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Warehouse</th>
                  <th style={{ textAlign: 'right' }}>Balance</th>
                  <th>Level</th>
                </tr>
              </thead>
              <tbody>
                {(balancesQuery.data ?? []).map((b) => {
                  const level = reorderByProduct.get(b.productId);
                  const pct = level && level > 0 ? Math.min(100, Math.round((b.balance / (level * 2)) * 100)) : 100;
                  const low = level != null && b.balance <= level;
                  return (
                    <tr key={`${b.productId}-${b.warehouseId}`}>
                      <td style={{ color: 'var(--text)' }}>{b.productName}</td>
                      <td>{b.warehouseName}</td>
                      <td style={{ textAlign: 'right' }} className="tabular">
                        {b.balance}
                      </td>
                      <td>
                        <div className="bar-track">
                          <div className={`bar-fill${low ? ' danger' : ''}`} style={{ width: `${pct}%` }} />
                        </div>
                      </td>
                    </tr>
                  );
                })}
                {(balancesQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={4} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No stock movements recorded yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="page-sub" style={{ marginTop: 10 }}>
            Recipe &amp; Service items aren't listed here — a Recipe's stock lives in its BOM ingredients; Services never carry stock.
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <div className="card-title">Reorder Suggestions</div>
          </div>
          {(reorderQuery.data ?? []).length > 0 ? (
            <div className="stack">
              {(reorderQuery.data ?? []).map((r) => (
                <div className="alert-row" key={r.productId}>
                  <div>
                    <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text)' }}>{r.productName}</div>
                    <div className="page-sub">
                      Reorder level {r.reorderLevel} · Balance {r.currentBalance}
                    </div>
                  </div>
                  <span className="badge badge-danger">short {r.shortBy}</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="empty-state">Everything's above its reorder level.</div>
          )}
        </div>
      </div>
    </>
  );
}
