import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AppShell } from '../components/AppShell';
import { SearchIcon } from '../components/icons';
import { listCategories, listProducts, listTables } from '../api/masters';
import {
  addLine,
  addPayment,
  applyDiscountOffer,
  applyManualDiscount,
  cancelOrder,
  clearDiscount,
  createOrder,
  holdOrder,
  listDiscountOffers,
  listOpenOrders,
  printKot,
  removeLine,
  updateLine,
} from '../api/sales';
import { apiErrorMessage } from '../api/client';
import type { DiscountOfferDto, OrderDto, OrderType, ProductDto } from '../api/types';

const ORDER_TYPES: { value: OrderType; label: string }[] = [
  { value: 'DineIn', label: 'Dine-in' },
  { value: 'Takeaway', label: 'Takeaway' },
  { value: 'Delivery', label: 'Delivery' },
];

const PAYMENT_MODES: { value: string; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Card', label: 'Card' },
  { value: 'ESewa', label: 'eSewa' },
  { value: 'Khalti', label: 'Khalti' },
  { value: 'BankTransfer', label: 'Bank' },
];

function formatRs(n: number) {
  return `Rs. ${n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

// A stable, deterministic "photo" so every product card/cart row gets a
// distinct-looking gradient without needing real product images yet.
function gradientFor(seed: string) {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  const hue = hash % 360;
  return `linear-gradient(150deg, oklch(72% 0.11 ${hue}), oklch(46% 0.08 ${hue}))`;
}

export function PosPage() {
  const queryClient = useQueryClient();

  const [orderType, setOrderType] = useState<OrderType>('DineIn');
  const [tableId, setTableId] = useState<string | null>(null);
  const [currentOrderId, setCurrentOrderId] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [paymentMode, setPaymentMode] = useState('Cash');
  const [showDiscount, setShowDiscount] = useState(false);
  const [showOpenOrders, setShowOpenOrders] = useState(false);
  const [banner, setBanner] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);

  const productsQuery = useQuery({ queryKey: ['products'], queryFn: () => listProducts() });
  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: listCategories });
  const tablesQuery = useQuery({ queryKey: ['tables'], queryFn: () => listTables() });
  const offersQuery = useQuery({ queryKey: ['discount-offers'], queryFn: () => listDiscountOffers(true) });
  const openOrdersQuery = useQuery({ queryKey: ['open-orders'], queryFn: listOpenOrders, enabled: showOpenOrders });

  const orderQuery = useQuery({
    queryKey: ['order', currentOrderId],
    queryFn: async () => {
      const orders = await listOpenOrders();
      return orders.find((o) => o.id === currentOrderId) ?? null;
    },
    enabled: currentOrderId !== null,
  });
  const order = orderQuery.data ?? null;

  function fail(err: unknown) {
    setBanner({ kind: 'error', text: apiErrorMessage(err) });
  }
  function refreshOrder(updated: OrderDto) {
    queryClient.setQueryData(['order', updated.id], updated);
    queryClient.invalidateQueries({ queryKey: ['open-orders'] });
  }

  const addLineMutation = useMutation({
    mutationFn: async (product: ProductDto) => {
      if (!order) {
        if (orderType === 'DineIn' && !tableId) throw new Error('Select a table first.');
        const created = await createOrder({ orderType, tableId: orderType === 'DineIn' ? tableId : null });
        setCurrentOrderId(created.id);
        return addLine(created.id, { productId: product.id, quantity: 1 });
      }
      const existing = order.lines.find((l) => l.productId === product.id);
      if (existing) return updateLine(order.id, existing.id, { quantity: existing.quantity + 1 });
      return addLine(order.id, { productId: product.id, quantity: 1 });
    },
    onSuccess: refreshOrder,
    onError: fail,
  });

  const stepMutation = useMutation({
    mutationFn: async ({ lineId, delta }: { lineId: string; delta: number }) => {
      if (!order) throw new Error('No active order.');
      const line = order.lines.find((l) => l.id === lineId);
      if (!line) throw new Error('Line not found.');
      const newQty = line.quantity + delta;
      if (newQty <= 0) return removeLine(order.id, lineId);
      return updateLine(order.id, lineId, { quantity: newQty });
    },
    onSuccess: refreshOrder,
    onError: fail,
  });

  const discountOfferMutation = useMutation({
    mutationFn: (offerId: string) => applyDiscountOffer(order!.id, offerId),
    onSuccess: (updated) => {
      refreshOrder(updated);
      setShowDiscount(false);
    },
    onError: fail,
  });
  const manualDiscountMutation = useMutation({
    mutationFn: (value: number) => applyManualDiscount(order!.id, 'Percent', value),
    onSuccess: (updated) => {
      refreshOrder(updated);
      setShowDiscount(false);
    },
    onError: fail,
  });
  const clearDiscountMutation = useMutation({
    mutationFn: () => clearDiscount(order!.id),
    onSuccess: refreshOrder,
    onError: fail,
  });

  const chargeMutation = useMutation({
    mutationFn: () => addPayment(order!.id, { amount: order!.amountRemaining, paymentMode }),
    onSuccess: (updated) => {
      refreshOrder(updated);
      if (updated.status === 'Paid') {
        setBanner({ kind: 'success', text: `Order ${updated.orderNumber} closed — Rs. ${updated.grandTotalAmount.toFixed(2)} received.` });
        setCurrentOrderId(null);
        setTableId(null);
      }
    },
    onError: fail,
  });

  const holdMutation = useMutation({
    mutationFn: () => holdOrder(order!.id),
    onSuccess: () => {
      setBanner({ kind: 'success', text: 'Order put on hold.' });
      setCurrentOrderId(null);
      queryClient.invalidateQueries({ queryKey: ['open-orders'] });
    },
    onError: fail,
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelOrder(order!.id),
    onSuccess: () => {
      setBanner({ kind: 'success', text: 'Order cancelled.' });
      setCurrentOrderId(null);
      queryClient.invalidateQueries({ queryKey: ['open-orders'] });
    },
    onError: fail,
  });

  const kotMutation = useMutation({
    mutationFn: () => printKot(order!.id),
    onSuccess: () => {
      setBanner({ kind: 'success', text: 'KOT sent to kitchen/bar.' });
      queryClient.invalidateQueries({ queryKey: ['order', order?.id] });
      orderQuery.refetch();
    },
    onError: fail,
  });

  const filteredProducts = useMemo(() => {
    const list = productsQuery.data ?? [];
    return list.filter((p) => {
      if (!p.trackInPos || !p.isActive || p.productType === 'Consumable') return false;
      if (categoryId && p.categoryId !== categoryId) return false;
      if (search && !p.name.toLowerCase().includes(search.toLowerCase())) return false;
      return true;
    });
  }, [productsQuery.data, categoryId, search]);

  const lineQtyByProduct = useMemo(() => {
    const map = new Map<string, number>();
    order?.lines.forEach((l) => map.set(l.productId, (map.get(l.productId) ?? 0) + l.quantity));
    return map;
  }, [order]);

  return (
    <AppShell title="Billing (POS)" subtitle="Ring up a sale">
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14, flex: 1, minHeight: 0 }}>
        {banner && (
          <div
            className="card"
            style={{
              padding: '10px 16px',
              borderColor: banner.kind === 'error' ? 'var(--danger)' : 'var(--success)',
              color: banner.kind === 'error' ? 'var(--danger)' : 'var(--success)',
              display: 'flex',
              justifyContent: 'space-between',
            }}
          >
            <span>{banner.text}</span>
            <button className="btn-ghost" style={{ border: 'none', background: 'none' }} onClick={() => setBanner(null)}>
              ✕
            </button>
          </div>
        )}

        <div className="pos-main" style={{ height: '100%' }}>
          <div className="pos-left">
            <div className="pos-top">
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                <div className="order-tabs">
                  {ORDER_TYPES.map((t) => (
                    <button
                      key={t.value}
                      className={`order-tab${orderType === t.value ? ' on' : ''}`}
                      disabled={!!order}
                      onClick={() => setOrderType(t.value)}
                    >
                      {t.label}
                    </button>
                  ))}
                </div>
                {orderType === 'DineIn' && !order && (
                  <select className="input" style={{ width: 170 }} value={tableId ?? ''} onChange={(e) => setTableId(e.target.value || null)}>
                    <option value="">Select table…</option>
                    {(tablesQuery.data ?? [])
                      .filter((t) => t.status === 'Vacant')
                      .map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.tableNumber} ({t.seats} seats)
                        </option>
                      ))}
                  </select>
                )}
                {order && <div className="chip">#{order.orderNumber}</div>}
                {order?.tableNumber && <div className="chip">Table {order.tableNumber}</div>}
                <div style={{ position: 'relative' }}>
                  <button className="chip" onClick={() => setShowOpenOrders((v) => !v)}>
                    Open Orders ({(openOrdersQuery.data ?? []).length || '…'})
                  </button>
                  {showOpenOrders && (
                    <div className="card" style={{ position: 'absolute', top: 40, left: 0, width: 260, zIndex: 10, maxHeight: 320, overflow: 'auto' }}>
                      {(openOrdersQuery.data ?? []).length === 0 && <div className="page-sub">No open orders.</div>}
                      {(openOrdersQuery.data ?? []).map((o) => (
                        <button
                          key={o.id}
                          className="alert-row"
                          style={{ width: '100%', background: 'none', border: 'none', cursor: 'pointer' }}
                          onClick={() => {
                            setCurrentOrderId(o.id);
                            setOrderType(o.orderType);
                            setShowOpenOrders(false);
                          }}
                        >
                          <span>
                            #{o.orderNumber} <span className="badge badge-gold">{o.status}</span>
                          </span>
                          <span className="tabular">{formatRs(o.grandTotalAmount)}</span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div className="search-pill">
                  <SearchIcon />
                  <input placeholder="Search product" value={search} onChange={(e) => setSearch(e.target.value)} />
                </div>
              </div>
            </div>

            <div className="cats">
              <button className={`cat-pill${categoryId === null ? ' on' : ''}`} onClick={() => setCategoryId(null)}>
                All Items
              </button>
              {(categoriesQuery.data ?? []).map((c) => (
                <button key={c.id} className={`cat-pill${categoryId === c.id ? ' on' : ''}`} onClick={() => setCategoryId(c.id)}>
                  {c.name}
                </button>
              ))}
            </div>

            {productsQuery.isLoading ? (
              <div className="empty-state">
                <span className="spinner" /> Loading products…
              </div>
            ) : filteredProducts.length === 0 ? (
              <div className="empty-state">No products match.</div>
            ) : (
              <div className="pgrid">
                {filteredProducts.map((p) => {
                  const qty = lineQtyByProduct.get(p.id);
                  return (
                    <button key={p.id} className="pcard" onClick={() => addLineMutation.mutate(p)} disabled={addLineMutation.isPending}>
                      {p.productType === 'Recipe' && <div className="precipe">Recipe</div>}
                      {qty ? <div className="qbadge">{qty}</div> : null}
                      <div className="pimg" style={{ background: gradientFor(p.name) }} />
                      <div className="pname">{p.name}</div>
                      <div className="pmeta">{p.categoryName ?? p.productType}</div>
                      <div className="pprice">Rs. {p.salePrice}</div>
                    </button>
                  );
                })}
              </div>
            )}
          </div>

          <div className="cart">
            <div className="cart-head">
              <div className="cart-title">Current Order</div>
              {order && (
                <button className="btn-ghost" style={{ border: 'none', background: 'none', color: 'var(--danger)', fontSize: 12 }} onClick={() => cancelMutation.mutate()}>
                  Cancel
                </button>
              )}
            </div>
            <div className="cart-cust">
              {order ? `${order.lines.length} item${order.lines.length === 1 ? '' : 's'}` : 'No order started — tap a product to begin'}
            </div>
            <div className="cart-items">
              {order?.lines.map((line) => (
                <div className="citem" key={line.id}>
                  <div className="cimg" style={{ background: gradientFor(line.productName) }} />
                  <div style={{ flex: 1 }}>
                    <div className="cname">{line.productName}</div>
                    <div className="cunit">{formatRs(line.unitPrice)} / unit</div>
                  </div>
                  <div className="stepper">
                    <button className="step-btn" onClick={() => stepMutation.mutate({ lineId: line.id, delta: -1 })}>
                      –
                    </button>
                    <span className="cqty">{line.quantity}</span>
                    <button className="step-btn" onClick={() => stepMutation.mutate({ lineId: line.id, delta: 1 })}>
                      +
                    </button>
                  </div>
                  <div className="cline tabular">{formatRs(line.lineTotalAmount)}</div>
                </div>
              ))}
            </div>

            <div className="summary">
              <div className="srow">
                <span>Subtotal</span>
                <span className="tabular">{formatRs(order?.subTotalAmount ?? 0)}</span>
              </div>
              <div className="srow">
                <span>
                  Discount{' '}
                  {order && order.discountAmount > 0 ? (
                    <a onClick={() => clearDiscountMutation.mutate()} style={{ cursor: 'pointer', fontSize: 11 }}>
                      (clear)
                    </a>
                  ) : (
                    <a onClick={() => setShowDiscount((v) => !v)} style={{ cursor: 'pointer', fontSize: 11 }}>
                      (+ add)
                    </a>
                  )}
                </span>
                <span className="tabular" style={{ color: 'var(--success)' }}>
                  {order && order.discountAmount > 0 ? `– ${formatRs(order.discountAmount)}` : formatRs(0)}
                </span>
              </div>
              {showDiscount && (
                <div className="card" style={{ padding: 12 }}>
                  <div style={{ fontSize: 11.5, color: 'var(--text-faint)', marginBottom: 8 }}>Saved offers</div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 10 }}>
                    {(offersQuery.data ?? []).map((o: DiscountOfferDto) => (
                      <button key={o.id} className="btn-ghost" style={{ justifyContent: 'space-between' }} onClick={() => discountOfferMutation.mutate(o.id)}>
                        <span>{o.name}</span>
                        <span>{o.discountType === 'Percent' ? `${o.value}%` : formatRs(o.value)}</span>
                      </button>
                    ))}
                    {(offersQuery.data ?? []).length === 0 && <div style={{ fontSize: 12, color: 'var(--text-faint)' }}>No saved offers.</div>}
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-faint)', marginBottom: 8 }}>Or manual % off</div>
                  <div style={{ display: 'flex', gap: 6 }}>
                    {[5, 10, 15, 20].map((pct) => (
                      <button key={pct} className="btn-ghost" style={{ flex: 1 }} onClick={() => manualDiscountMutation.mutate(pct)}>
                        {pct}%
                      </button>
                    ))}
                  </div>
                </div>
              )}
              <div className="srow">
                <span>VAT</span>
                <span className="tabular">{formatRs(order?.vatAmount ?? 0)}</span>
              </div>
              {order && order.roundOffAmount !== 0 && (
                <div className="srow">
                  <span>Round Off</span>
                  <span className="tabular">{formatRs(order.roundOffAmount)}</span>
                </div>
              )}
              <div className="srow total">
                <span>Total Payable</span>
                <span className="val tabular">{formatRs(order?.grandTotalAmount ?? 0)}</span>
              </div>
              {order && order.amountPaid > 0 && (
                <div className="srow">
                  <span>Paid / Remaining</span>
                  <span className="tabular">
                    {formatRs(order.amountPaid)} / {formatRs(order.amountRemaining)}
                  </span>
                </div>
              )}
            </div>

            <div className="pay-methods">
              {PAYMENT_MODES.map((pm) => (
                <button key={pm.value} className={`pm${paymentMode === pm.value ? ' on' : ''}`} onClick={() => setPaymentMode(pm.value)}>
                  {pm.label}
                </button>
              ))}
            </div>

            <div className="cart-actions">
              <button
                className="btn btn-primary btn-block"
                disabled={!order || order.lines.length === 0 || chargeMutation.isPending}
                onClick={() => chargeMutation.mutate()}
              >
                {chargeMutation.isPending ? <span className="spinner" /> : `Charge ${formatRs(order?.amountRemaining ?? 0)}`}
              </button>
              <div style={{ display: 'flex', gap: 10 }}>
                <button className="btn btn-ghost" style={{ flex: 1 }} disabled={!order} onClick={() => holdMutation.mutate()}>
                  Hold Order
                </button>
                <button className="btn btn-ghost" style={{ flex: 1 }} disabled={!order} onClick={() => kotMutation.mutate()}>
                  Print KOT
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  );
}
