import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listProducts, listWarehouses } from '../../api/masters';
import { cancelTransfer, createTransfer, listTransfers, postTransfer } from '../../api/inventory';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, todayIso } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Pending: 'badge-gold', Completed: 'badge-success', Cancelled: 'badge-neutral' };

export function TransfersTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [productId, setProductId] = useState('');
  const [fromWarehouseId, setFromWarehouseId] = useState('');
  const [toWarehouseId, setToWarehouseId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [date, setDate] = useState(todayIso());

  const productsQuery = useQuery({ queryKey: ['products'], queryFn: () => listProducts() });
  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: listWarehouses });
  const transfersQuery = useQuery({ queryKey: ['transfers'], queryFn: () => listTransfers() });

  const stockedProducts = (productsQuery.data ?? []).filter((p) => p.productType === 'Inventory' || p.productType === 'Consumable');

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['transfers'] });
    queryClient.invalidateQueries({ queryKey: ['stock-balances'] });
  }

  const createMutation = useMutation({
    mutationFn: () => {
      if (!productId || !fromWarehouseId || !toWarehouseId) throw new Error('Item and both warehouses are required.');
      if (fromWarehouseId === toWarehouseId) throw new Error('Source and destination warehouse must differ.');
      const qty = Number(quantity);
      if (!qty || qty <= 0) throw new Error('Quantity must be positive.');
      return createTransfer({ productId, fromWarehouseId, toWarehouseId, quantity: qty, transferDate: date });
    },
    onSuccess: () => {
      invalidate();
      succeed('Transfer created as Pending — post it to move stock.');
      setQuantity('');
    },
    onError: fail,
  });

  const postMutation = useMutation({
    mutationFn: (id: string) => postTransfer(id),
    onSuccess: () => {
      invalidate();
      succeed('Transfer posted — stock moved.');
    },
    onError: fail,
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => cancelTransfer(id),
    onSuccess: () => {
      invalidate();
      succeed('Transfer cancelled.');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Stock Transfers</div>
            <span className="chip">{(transfersQuery.data ?? []).length} total</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Item</th>
                  <th>From → To</th>
                  <th style={{ textAlign: 'right' }}>Qty</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {(transfersQuery.data ?? []).map((t) => (
                  <tr key={t.id}>
                    <td>{formatDate(t.transferDate)}</td>
                    <td style={{ color: 'var(--text)' }}>{t.productName}</td>
                    <td>
                      {t.fromWarehouseName} → {t.toWarehouseName}
                    </td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {t.quantity}
                    </td>
                    <td>
                      <span className={`badge ${STATUS_BADGE[t.status]}`}>{t.status}</span>
                    </td>
                    <td>
                      {t.status === 'Pending' && (
                        <div style={{ display: 'flex', gap: 6 }}>
                          <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} onClick={() => postMutation.mutate(t.id)}>
                            Post
                          </button>
                          <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} onClick={() => cancelMutation.mutate(t.id)}>
                            Cancel
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
                {(transfersQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={6} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No transfers yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">New Transfer</div>
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
              <label>From</label>
              <select className="input" value={fromWarehouseId} onChange={(e) => setFromWarehouseId(e.target.value)}>
                <option value="">Select…</option>
                {(warehousesQuery.data ?? []).map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>To</label>
              <select className="input" value={toWarehouseId} onChange={(e) => setToWarehouseId(e.target.value)}>
                <option value="">Select…</option>
                {(warehousesQuery.data ?? []).map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="frow">
            <div className="field">
              <label>Quantity</label>
              <input className="input" type="number" step="0.001" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
            </div>
            <div className="field">
              <label>Date</label>
              <input className="input" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Create Transfer'}
          </button>
        </div>
      </div>
    </>
  );
}
