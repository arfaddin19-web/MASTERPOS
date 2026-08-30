import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listProducts, listWarehouses } from '../../api/masters';
import { getLedger } from '../../api/inventory';
import { formatDate } from '../../lib/format';

export function LedgerTab() {
  const [productId, setProductId] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  const productsQuery = useQuery({ queryKey: ['products'], queryFn: () => listProducts() });
  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: listWarehouses });
  const ledgerQuery = useQuery({
    queryKey: ['ledger', productId, warehouseId, fromDate, toDate],
    queryFn: () => getLedger({ productId: productId || undefined, warehouseId: warehouseId || undefined, fromDate: fromDate || undefined, toDate: toDate || undefined }),
  });

  return (
    <>
      <div className="card">
        <div className="card-head">
          <div className="card-title">Stock Ledger</div>
          <span className="chip">{(ledgerQuery.data ?? []).length} entries</span>
        </div>
        <div className="header-grid" style={{ marginBottom: 16 }}>
          <div className="field">
            <label>Item</label>
            <select className="input" value={productId} onChange={(e) => setProductId(e.target.value)}>
              <option value="">All items</option>
              {(productsQuery.data ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Warehouse</label>
            <select className="input" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
              <option value="">All warehouses</option>
              {(warehousesQuery.data ?? []).map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>From</label>
            <input className="input" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </div>
          <div className="field">
            <label>To</label>
            <input className="input" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </div>
        </div>
        <div className="scroll-x">
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Item</th>
                <th>Warehouse</th>
                <th style={{ textAlign: 'right' }}>In</th>
                <th style={{ textAlign: 'right' }}>Out</th>
                <th style={{ textAlign: 'right' }}>Balance</th>
                <th>Reference</th>
              </tr>
            </thead>
            <tbody>
              {(ledgerQuery.data ?? []).map((e) => (
                <tr key={e.id}>
                  <td>{formatDate(e.movementDate)}</td>
                  <td style={{ color: 'var(--text)' }}>{e.productName}</td>
                  <td>{e.warehouseName}</td>
                  <td style={{ textAlign: 'right', color: e.quantityIn > 0 ? 'var(--success)' : undefined }} className="tabular">
                    {e.quantityIn > 0 ? e.quantityIn : '—'}
                  </td>
                  <td style={{ textAlign: 'right', color: e.quantityOut > 0 ? 'var(--danger)' : undefined }} className="tabular">
                    {e.quantityOut > 0 ? e.quantityOut : '—'}
                  </td>
                  <td style={{ textAlign: 'right', color: 'var(--text)' }} className="tabular">
                    {e.runningBalance}
                  </td>
                  <td>
                    <span className="badge badge-neutral">{e.referenceType}</span>
                  </td>
                </tr>
              ))}
              {(ledgerQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={7} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No movements match these filters.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
