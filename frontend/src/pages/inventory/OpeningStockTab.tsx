import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listProducts, listWarehouses } from '../../api/masters';
import { createOpeningStock, listOpeningStock } from '../../api/inventory';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, formatRs, todayIso } from '../../lib/format';

export function OpeningStockTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [warehouseId, setWarehouseId] = useState('');
  const [productId, setProductId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [unitCost, setUnitCost] = useState('');
  const [date, setDate] = useState(todayIso());

  const productsQuery = useQuery({ queryKey: ['products'], queryFn: () => listProducts() });
  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: listWarehouses });
  const openingQuery = useQuery({ queryKey: ['opening-stock'], queryFn: listOpeningStock });

  const stockedProducts = (productsQuery.data ?? []).filter((p) => p.productType === 'Inventory' || p.productType === 'Consumable');

  const createMutation = useMutation({
    mutationFn: () => {
      if (!warehouseId || !productId) throw new Error('Warehouse and product are required.');
      const qty = Number(quantity);
      if (!qty || qty <= 0) throw new Error('Quantity must be positive.');
      return createOpeningStock({ warehouseId, productId, quantity: qty, unitCost: Number(unitCost) || 0, asOfDate: date });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['opening-stock'] });
      queryClient.invalidateQueries({ queryKey: ['stock-balances'] });
      succeed('Opening stock recorded.');
      setQuantity('');
      setUnitCost('');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Opening Stock</div>
            <span className="chip">{(openingQuery.data ?? []).length} recorded</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>As Of</th>
                  <th>Item</th>
                  <th>Warehouse</th>
                  <th style={{ textAlign: 'right' }}>Qty</th>
                  <th style={{ textAlign: 'right' }}>Unit Cost</th>
                </tr>
              </thead>
              <tbody>
                {(openingQuery.data ?? []).map((o) => (
                  <tr key={o.id}>
                    <td>{formatDate(o.asOfDate)}</td>
                    <td style={{ color: 'var(--text)' }}>{o.productName}</td>
                    <td>{o.warehouseName}</td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {o.quantity}
                    </td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(o.unitCost)}
                    </td>
                  </tr>
                ))}
                {(openingQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No opening balances recorded yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">Record Opening Stock</div>
          <div className="page-sub">One-time starting balance per product/warehouse — correct a mistake with an Adjustment afterward, not a second entry here.</div>
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
              <label>Quantity</label>
              <input className="input" type="number" step="0.001" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
            </div>
            <div className="field">
              <label>Unit Cost</label>
              <input className="input" type="number" step="0.01" value={unitCost} onChange={(e) => setUnitCost(e.target.value)} />
            </div>
          </div>
          <div className="field">
            <label>As Of Date</label>
            <input className="input" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Record Opening Stock'}
          </button>
        </div>
      </div>
    </>
  );
}
