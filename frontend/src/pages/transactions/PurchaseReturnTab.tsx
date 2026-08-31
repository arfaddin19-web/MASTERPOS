import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listParties, listProducts, listUnits } from '../../api/masters';
import { addReturnLine, cancelReturn, createReturn, getReturn, listInvoices, listReturns, postReturn, removeReturnLine } from '../../api/purchase';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, formatRs, todayIso } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Draft: 'badge-gold', Posted: 'badge-success', Cancelled: 'badge-neutral' };

export function PurchaseReturnTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [showNew, setShowNew] = useState(false);
  const [supplierId, setSupplierId] = useState('');
  const [originalInvoiceId, setOriginalInvoiceId] = useState('');
  const [returnDate, setReturnDate] = useState(todayIso());

  const [lineProductId, setLineProductId] = useState('');
  const [lineUnitId, setLineUnitId] = useState('');
  const [lineQty, setLineQty] = useState('1');
  const [lineRate, setLineRate] = useState('0');
  const [lineVat, setLineVat] = useState('13');

  const suppliersQuery = useQuery({ queryKey: ['suppliers'], queryFn: () => listParties({ activeOnly: true }) });
  const productsQuery = useQuery({ queryKey: ['products'], queryFn: () => listProducts() });
  const unitsQuery = useQuery({ queryKey: ['units'], queryFn: listUnits });
  const postedInvoicesQuery = useQuery({ queryKey: ['purchase-invoices', 'Posted'], queryFn: () => listInvoices('Posted') });
  const returnsQuery = useQuery({ queryKey: ['purchase-returns'], queryFn: () => listReturns() });
  const returnQuery = useQuery({ queryKey: ['purchase-return', selectedId], queryFn: () => getReturn(selectedId!), enabled: !!selectedId });

  const purchasable = (productsQuery.data ?? []).filter((p) => p.productType === 'Inventory' || p.productType === 'Consumable');
  const suppliers = (suppliersQuery.data ?? []).filter((p) => p.partyType === 'Supplier' || p.partyType === 'Both');

  function invalidateList() {
    queryClient.invalidateQueries({ queryKey: ['purchase-returns'] });
  }
  function refreshDetail(id: string) {
    queryClient.invalidateQueries({ queryKey: ['purchase-return', id] });
    invalidateList();
  }

  const createMutation = useMutation({
    mutationFn: () => {
      if (!supplierId) throw new Error('Select a supplier.');
      return createReturn({ supplierId, originalPurchaseInvoiceId: originalInvoiceId || null, returnDate });
    },
    onSuccess: (created) => {
      invalidateList();
      setSelectedId(created.id);
      setShowNew(false);
      succeed(`Draft ${created.returnNumber} created — add line items below.`);
    },
    onError: fail,
  });

  const addLineMutation = useMutation({
    mutationFn: () => {
      if (!selectedId) throw new Error('No return selected.');
      if (!lineProductId || !lineUnitId) throw new Error('Item and unit are required.');
      return addReturnLine(selectedId, { productId: lineProductId, unitId: lineUnitId, quantity: Number(lineQty) || 0, rate: Number(lineRate) || 0, vatPercent: Number(lineVat) || 0 });
    },
    onSuccess: () => {
      refreshDetail(selectedId!);
      setLineProductId('');
      setLineQty('1');
      setLineRate('0');
    },
    onError: fail,
  });

  const removeLineMutation = useMutation({
    mutationFn: (lineId: string) => removeReturnLine(selectedId!, lineId),
    onSuccess: () => refreshDetail(selectedId!),
    onError: fail,
  });

  const postMutation = useMutation({
    mutationFn: () => postReturn(selectedId!),
    onSuccess: () => {
      refreshDetail(selectedId!);
      succeed('Return posted — stock removed.');
    },
    onError: fail,
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelReturn(selectedId!),
    onSuccess: () => {
      refreshDetail(selectedId!);
      succeed('Return cancelled.');
    },
    onError: fail,
  });

  const ret = returnQuery.data ?? null;

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div className="chip">{(returnsQuery.data ?? []).length} Returns</div>
        <button
          className="btn btn-primary"
          onClick={() => {
            setShowNew(true);
            setSelectedId(null);
            setSupplierId('');
            setOriginalInvoiceId('');
            setReturnDate(todayIso());
          }}
        >
          + New Purchase Return
        </button>
      </div>

      <div className="split split-wide">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th>Return #</th>
                <th>Supplier</th>
                <th>Date</th>
                <th style={{ textAlign: 'right' }}>Total</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {(returnsQuery.data ?? []).map((r) => (
                <tr
                  key={r.id}
                  className={`row-clickable${r.id === selectedId ? ' row-selected' : ''}`}
                  onClick={() => {
                    setSelectedId(r.id);
                    setShowNew(false);
                  }}
                >
                  <td style={{ color: 'var(--text)' }}>{r.returnNumber}</td>
                  <td>{r.supplierName}</td>
                  <td>{formatDate(r.returnDate)}</td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    {formatRs(r.grandTotalAmount)}
                  </td>
                  <td>
                    <span className={`badge ${STATUS_BADGE[r.status]}`}>{r.status}</span>
                  </td>
                </tr>
              ))}
              {(returnsQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No purchase returns yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {showNew && (
          <div className="form-card">
            <div className="form-card-title">New Purchase Return</div>
            <div className="field">
              <label>Supplier</label>
              <select className="input" value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
                <option value="">Select…</option>
                {suppliers.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Against Original Invoice (optional)</label>
              <select className="input" value={originalInvoiceId} onChange={(e) => setOriginalInvoiceId(e.target.value)}>
                <option value="">— Standalone return —</option>
                {(postedInvoicesQuery.data ?? [])
                  .filter((i) => !supplierId || i.supplierId === supplierId)
                  .map((i) => (
                    <option key={i.id} value={i.id}>
                      {i.invoiceNumber}
                    </option>
                  ))}
              </select>
            </div>
            <div className="field">
              <label>Return Date</label>
              <input className="input" type="date" value={returnDate} onChange={(e) => setReturnDate(e.target.value)} />
            </div>
            <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
              {createMutation.isPending ? <span className="spinner" /> : 'Create Draft'}
            </button>
          </div>
        )}

        {!showNew && ret && (
          <div className="form-card" style={{ gap: 20 }}>
            <div className="form-head">
              <div className="form-card-title">
                {ret.returnNumber} <span className={`badge ${STATUS_BADGE[ret.status]}`} style={{ marginLeft: 8 }}>{ret.status}</span>
              </div>
              <button className="close-x" onClick={() => setSelectedId(null)}>
                ✕
              </button>
            </div>
            <div className="page-sub">
              {ret.supplierName} · {formatDate(ret.returnDate)}
            </div>

            <div className="scroll-x">
              <table className="doc-lines-table">
                <thead>
                  <tr>
                    <th>Item</th>
                    <th>Unit</th>
                    <th style={{ textAlign: 'right' }}>Qty</th>
                    <th style={{ textAlign: 'right' }}>Rate</th>
                    <th style={{ textAlign: 'right' }}>VAT%</th>
                    <th style={{ textAlign: 'right' }}>Amount</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {ret.lines.map((l) => (
                    <tr key={l.id}>
                      <td style={{ color: 'var(--text)' }}>{l.productName}</td>
                      <td>{l.unitName}</td>
                      <td style={{ textAlign: 'right' }} className="tabular">
                        {l.quantity}
                      </td>
                      <td style={{ textAlign: 'right' }} className="tabular">
                        {formatRs(l.rate)}
                      </td>
                      <td style={{ textAlign: 'right' }} className="tabular">
                        {l.vatPercent}%
                      </td>
                      <td style={{ textAlign: 'right', color: 'var(--text)' }} className="tabular">
                        {formatRs(l.lineAmount)}
                      </td>
                      <td>
                        {ret.status === 'Draft' && (
                          <button className="close-x" onClick={() => removeLineMutation.mutate(l.id)}>
                            ✕
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                  {ret.status === 'Draft' && (
                    <tr>
                      <td>
                        <select className="input mini-input" style={{ width: '100%', textAlign: 'left' }} value={lineProductId} onChange={(e) => setLineProductId(e.target.value)}>
                          <option value="">+ Add item…</option>
                          {purchasable.map((p) => (
                            <option key={p.id} value={p.id}>
                              {p.name}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <select className="input mini-input" style={{ width: '100%' }} value={lineUnitId} onChange={(e) => setLineUnitId(e.target.value)}>
                          <option value="">—</option>
                          {(unitsQuery.data ?? []).map((u) => (
                            <option key={u.id} value={u.id}>
                              {u.name}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <input className="mini-input" type="number" step="0.001" value={lineQty} onChange={(e) => setLineQty(e.target.value)} />
                      </td>
                      <td>
                        <input className="mini-input" type="number" step="0.01" value={lineRate} onChange={(e) => setLineRate(e.target.value)} />
                      </td>
                      <td>
                        <input className="mini-input" type="number" step="0.01" value={lineVat} onChange={(e) => setLineVat(e.target.value)} />
                      </td>
                      <td colSpan={2}>
                        <button className="btn btn-ghost" style={{ padding: '6px 12px', fontSize: 11 }} disabled={addLineMutation.isPending} onClick={() => addLineMutation.mutate()}>
                          Add
                        </button>
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            <div className="summary" style={{ padding: 0, border: 'none' }}>
              <div className="srow">
                <span>Subtotal</span>
                <span className="tabular">{formatRs(ret.subTotalAmount)}</span>
              </div>
              <div className="srow">
                <span>VAT</span>
                <span className="tabular">{formatRs(ret.vatAmount)}</span>
              </div>
              <div className="srow total">
                <span>Grand Total</span>
                <span className="val tabular">{formatRs(ret.grandTotalAmount)}</span>
              </div>
            </div>

            <div className="form-foot">
              {ret.status === 'Draft' && (
                <>
                  <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={postMutation.isPending || ret.lines.length === 0} onClick={() => postMutation.mutate()}>
                    {postMutation.isPending ? <span className="spinner" /> : 'Post Return'}
                  </button>
                  <button className="btn btn-danger" onClick={() => cancelMutation.mutate()}>
                    Cancel
                  </button>
                </>
              )}
            </div>
          </div>
        )}
      </div>
    </>
  );
}
