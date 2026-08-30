import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listProducts, listWarehouses } from '../../api/masters';
import { createAdjustment, listAdjustments } from '../../api/inventory';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, todayIso } from '../../lib/format';

export function AdjustmentsTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [warehouseId, setWarehouseId] = useState('');
  const [productId, setProductId] = useState('');
  const [quantityChange, setQuantityChange] = useState('');
  const [reason, setReason] = useState('');
  const [date, setDate] = useState(todayIso());

  const productsQuery = useQuery({ queryKey: ['products'], queryFn: () => listProducts() });
  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: listWarehouses });
  const adjustmentsQuery = useQuery({ queryKey: ['adjustments'], queryFn: () => listAdjustments() });

  const stockedProducts = (productsQuery.data ?? []).filter((p) => p.productType === 'Inventory' || p.productType === 'Consumable');

  const createMutation = useMutation({
    mutationFn: () => {
      if (!warehouseId || !productId) throw new Error('Warehouse and product are required.');
      const qty = Number(quantityChange);
      if (!qty) throw new Error('Quantity change cannot be zero.');
      if (!reason.trim()) throw new Error('A reason is required.');
      return createAdjustment({ warehouseId, productId, quantityChange: qty, reason: reason.trim(), adjustmentDate: date });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['adjustments'] });
      queryClient.invalidateQueries({ queryKey: ['stock-balances'] });
      succeed('Adjustment posted.');
      setQuantityChange('');
      setReason('');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Stock Adjustments</div>
            <span className="chip">{(adjustmentsQuery.data ?? []).length} recorded</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Item</th>
                  <th>Warehouse</th>
                  <th style={{ textAlign: 'right' }}>Change</th>
                  <th>Reason</th>
                </tr>
              </thead>
              <tbody>
                {(adjustmentsQuery.data ?? []).map((a) => (
                  <tr key={a.id}>
                    <td>{formatDate(a.adjustmentDate)}</td>
                    <td style={{ color: 'var(--text)' }}>{a.productName}</td>
                    <td>{a.warehouseName}</td>
                    <td style={{ textAlign: 'right', color: a.quantityChange < 0 ? 'var(--danger)' : 'var(--success)' }} className="tabular">
                      {a.quantityChange > 0 ? '+' : ''}
                      {a.quantityChange}
                    </td>
                    <td>{a.reason}</td>
                  </tr>
                ))}
                {(adjustmentsQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No adjustments yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">Post Adjustment</div>
          <div className="field">
            <label>Warehouse</label>
            <select className="input" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
              <option value="">Select…</option>
              {(warehousesQuery.data ?? []).map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Item</label>
            <select className="input" value={productId} onChange={(e) => setProductId(e.target.value)}>
              <option value="">Select…</option>
              {stockedProducts.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </div>
          <div className="frow">
            <div className="field">
              <label>Quantity Change</label>
              <input className="input" type="number" step="0.001" placeholder="+10 or -5" value={quantityChange} onChange={(e) => setQuantityChange(e.target.value)} />
            </div>
            <div className="field">
              <label>Date</label>
              <input className="input" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
          </div>
          <div className="field">
            <label>Reason</label>
            <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Breakage, count correction" />
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Post Adjustment'}
          </button>
        </div>
      </div>
    </>
  );
}
