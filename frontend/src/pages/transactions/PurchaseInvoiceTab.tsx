import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listParties, listProducts, listUnits } from '../../api/masters';
import {
  addInvoiceLine,
  cancelInvoice,
  createInvoice,
  getInvoice,
  listInvoices,
  postInvoice,
  recordInvoicePayment,
  removeInvoiceLine,
} from '../../api/purchase';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, formatRs, todayIso } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Draft: 'badge-gold', Posted: 'badge-success', Cancelled: 'badge-neutral' };

export function PurchaseInvoiceTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();

  const [statusFilter, setStatusFilter] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [showNew, setShowNew] = useState(false);

  const [supplierId, setSupplierId] = useState('');
  const [refNo, setRefNo] = useState('');
  const [invoiceDate, setInvoiceDate] = useState(todayIso());
  const [paymentTerms, setPaymentTerms] = useState('');

  const [lineProductId, setLineProductId] = useState('');
  const [lineUnitId, setLineUnitId] = useState('');
  const [lineQty, setLineQty] = useState('1');
  const [lineRate, setLineRate] = useState('0');
  const [lineDiscount, setLineDiscount] = useState('0');
  const [lineVat, setLineVat] = useState('13');
  const [paymentAmount, setPaymentAmount] = useState('');

  const suppliersQuery = useQuery({ queryKey: ['suppliers'], queryFn: () => listParties({ partyType: undefined, activeOnly: true }) });
  const productsQuery = useQuery({ queryKey: ['products'], queryFn: () => listProducts() });
  const unitsQuery = useQuery({ queryKey: ['units'], queryFn: listUnits });
  const invoicesQuery = useQuery({ queryKey: ['purchase-invoices', statusFilter], queryFn: () => listInvoices(statusFilter || undefined) });
  const invoiceQuery = useQuery({ queryKey: ['purchase-invoice', selectedId], queryFn: () => getInvoice(selectedId!), enabled: !!selectedId });

  const purchasable = (productsQuery.data ?? []).filter((p) => p.productType === 'Inventory' || p.productType === 'Consumable');
  const suppliers = (suppliersQuery.data ?? []).filter((p) => p.partyType === 'Supplier' || p.partyType === 'Both');

  function invalidateList() {
    queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] });
  }
  function refreshDetail(id: string) {
    queryClient.invalidateQueries({ queryKey: ['purchase-invoice', id] });
    invalidateList();
  }

  const createMutation = useMutation({
    mutationFn: () => {
      if (!supplierId) throw new Error('Select a supplier.');
      return createInvoice({ supplierId, supplierReferenceNo: refNo.trim() || null, invoiceDate, paymentTerms: paymentTerms.trim() || null });
    },
    onSuccess: (created) => {
      invalidateList();
      setSelectedId(created.id);
      setShowNew(false);
      succeed(`Draft ${created.invoiceNumber} created — add line items below.`);
    },
    onError: fail,
  });

  const addLineMutation = useMutation({
    mutationFn: () => {
      if (!selectedId) throw new Error('No invoice selected.');
      if (!lineProductId || !lineUnitId) throw new Error('Item and unit are required.');
      return addInvoiceLine(selectedId, {
        productId: lineProductId,
        unitId: lineUnitId,
        quantity: Number(lineQty) || 0,
        rate: Number(lineRate) || 0,
        discountPercent: Number(lineDiscount) || 0,
        vatPercent: Number(lineVat) || 0,
      });
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
    mutationFn: (lineId: string) => removeInvoiceLine(selectedId!, lineId),
    onSuccess: () => refreshDetail(selectedId!),
    onError: fail,
  });

  const postMutation = useMutation({
    mutationFn: () => postInvoice(selectedId!),
    onSuccess: () => {
      refreshDetail(selectedId!);
      succeed('Invoice posted — stock received.');
    },
    onError: fail,
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelInvoice(selectedId!),
    onSuccess: () => {
      refreshDetail(selectedId!);
      succeed('Invoice cancelled.');
    },
    onError: fail,
  });

  const paymentMutation = useMutation({
    mutationFn: () => {
      const amount = Number(paymentAmount);
      if (!amount || amount <= 0) throw new Error('Enter a payment amount.');
      return recordInvoicePayment(selectedId!, amount);
    },
    onSuccess: () => {
      refreshDetail(selectedId!);
      succeed('Payment recorded.');
      setPaymentAmount('');
    },
    onError: fail,
  });

  const invoice = invoiceQuery.data ?? null;

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div style={{ display: 'flex', gap: 10 }}>
          <select className="input" style={{ width: 150 }} value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">All Status</option>
            <option value="Draft">Draft</option>
            <option value="Posted">Posted</option>
            <option value="Cancelled">Cancelled</option>
          </select>
          <div className="chip">{(invoicesQuery.data ?? []).length} Invoices</div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => {
            setShowNew(true);
            setSelectedId(null);
            setSupplierId('');
            setRefNo('');
            setPaymentTerms('');
            setInvoiceDate(todayIso());
          }}
        >
          + New Purchase Invoice
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th>Invoice #</th>
                <th>Supplier</th>
                <th>Date</th>
                <th style={{ textAlign: 'right' }}>Total</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {(invoicesQuery.data ?? []).map((inv) => (
                <tr
                  key={inv.id}
                  className={`row-clickable${inv.id === selectedId ? ' row-selected' : ''}`}
                  onClick={() => {
                    setSelectedId(inv.id);
                    setShowNew(false);
                  }}
                >
                  <td style={{ color: 'var(--text)' }}>{inv.invoiceNumber}</td>
                  <td>{inv.supplierName}</td>
                  <td>{formatDate(inv.invoiceDate)}</td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    {formatRs(inv.grandTotalAmount)}
                  </td>
                  <td>
                    <span className={`badge ${STATUS_BADGE[inv.status]}`}>{inv.status}</span>
                  </td>
                </tr>
              ))}
              {(invoicesQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No purchase invoices yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {showNew && (
          <div className="form-card">
            <div className="form-card-title">New Purchase Invoice</div>
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
            <div className="frow">
              <div className="field">
                <label>Invoice Date</label>
                <input className="input" type="date" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} />
              </div>
              <div className="field">
                <label>Reference No.</label>
                <input className="input" value={refNo} onChange={(e) => setRefNo(e.target.value)} />
              </div>
            </div>
            <div className="field">
              <label>Payment Terms</label>
              <input className="input" value={paymentTerms} onChange={(e) => setPaymentTerms(e.target.value)} placeholder="e.g. Net 15 Days" />
            </div>
            <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
              {createMutation.isPending ? <span className="spinner" /> : 'Create Draft'}
            </button>
          </div>
        )}

        {!showNew && invoice && (
          <div className="form-card" style={{ gap: 20 }}>
            <div className="form-head">
              <div className="form-card-title">
                {invoice.invoiceNumber} <span className={`badge ${STATUS_BADGE[invoice.status]}`} style={{ marginLeft: 8 }}>{invoice.status}</span>
              </div>
              <button className="close-x" onClick={() => setSelectedId(null)}>
                ✕
              </button>
            </div>
            <div className="page-sub">
              {invoice.supplierName} · {formatDate(invoice.invoiceDate)} {invoice.paymentTerms ? `· ${invoice.paymentTerms}` : ''}
            </div>

            <div className="scroll-x">
              <table className="doc-lines-table">
                <thead>
                  <tr>
                    <th>Item</th>
                    <th>Unit</th>
                    <th style={{ textAlign: 'right' }}>Qty</th>
                    <th style={{ textAlign: 'right' }}>Rate</th>
                    <th style={{ textAlign: 'right' }}>Disc%</th>
                    <th style={{ textAlign: 'right' }}>VAT%</th>
                    <th style={{ textAlign: 'right' }}>Amount</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {invoice.lines.map((l) => (
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
                        {l.discountPercent}%
                      </td>
                      <td style={{ textAlign: 'right' }} className="tabular">
                        {l.vatPercent}%
                      </td>
                      <td style={{ textAlign: 'right', color: 'var(--text)' }} className="tabular">
                        {formatRs(l.lineAmount)}
                      </td>
                      <td>
                        {invoice.status === 'Draft' && (
                          <button className="close-x" onClick={() => removeLineMutation.mutate(l.id)}>
                            ✕
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                  {invoice.status === 'Draft' && (
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
                        <input className="mini-input" type="number" step="0.01" value={lineDiscount} onChange={(e) => setLineDiscount(e.target.value)} />
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
                <span className="tabular">{formatRs(invoice.subTotalAmount)}</span>
              </div>
              <div className="srow">
                <span>Discount</span>
                <span className="tabular" style={{ color: 'var(--success)' }}>
                  – {formatRs(invoice.discountAmount)}
                </span>
              </div>
              <div className="srow">
                <span>VAT</span>
                <span className="tabular">{formatRs(invoice.vatAmount)}</span>
              </div>
              <div className="srow">
                <span>Round Off</span>
                <span className="tabular">{formatRs(invoice.roundOffAmount)}</span>
              </div>
              <div className="srow total">
                <span>Grand Total</span>
                <span className="val tabular">{formatRs(invoice.grandTotalAmount)}</span>
              </div>
              <div className="srow">
                <span>Paid / Remaining</span>
                <span className="tabular">
                  {formatRs(invoice.amountPaid)} / {formatRs(invoice.amountRemaining)}
                </span>
              </div>
            </div>

            {invoice.status !== 'Cancelled' && invoice.amountRemaining > 0 && (
              <div className="field-row">
                <input className="input" type="number" step="0.01" placeholder="Payment amount" value={paymentAmount} onChange={(e) => setPaymentAmount(e.target.value)} />
                <button className="btn btn-ghost" disabled={paymentMutation.isPending} onClick={() => paymentMutation.mutate()}>
                  Record Payment
                </button>
              </div>
            )}

            <div className="form-foot">
              {invoice.status === 'Draft' && (
                <>
                  <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={postMutation.isPending || invoice.lines.length === 0} onClick={() => postMutation.mutate()}>
                    {postMutation.isPending ? <span className="spinner" /> : 'Post Purchase Entry'}
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
