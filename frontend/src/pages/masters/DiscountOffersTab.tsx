import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createDiscountOffer,
  deleteDiscountOffer,
  listDiscountOffers,
  setDiscountOfferActive,
  updateDiscountOffer,
  type UpsertDiscountOfferRequest,
} from '../../api/sales';
import type { DiscountOfferDto } from '../../api/types';
import { Banner, Switch, useBanner } from '../../components/Shared';
import { formatDate } from '../../lib/format';

function blank(): UpsertDiscountOfferRequest {
  return { name: '', discountType: 'Percent', value: 10, validFrom: null, validTo: null };
}
function toForm(o: DiscountOfferDto): UpsertDiscountOfferRequest {
  return { name: o.name, discountType: o.discountType, value: o.value, validFrom: o.validFrom, validTo: o.validTo };
}

export function DiscountOffersTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<UpsertDiscountOfferRequest>(blank());

  const offersQuery = useQuery({ queryKey: ['masters-discount-offers'], queryFn: () => listDiscountOffers(false) });
  const selected = (offersQuery.data ?? []).find((o) => o.id === selectedId) ?? null;

  useEffect(() => {
    if (selected) setForm(toForm(selected));
  }, [selectedId]); // eslint-disable-line react-hooks/exhaustive-deps

  function startNew() {
    setSelectedId(null);
    setForm(blank());
    clear();
  }

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['masters-discount-offers'] });
    queryClient.invalidateQueries({ queryKey: ['discount-offers'] });
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!form.name.trim()) throw new Error('Name is required.');
      const payload: UpsertDiscountOfferRequest = { ...form, name: form.name.trim(), value: Number(form.value) || 0, validFrom: form.validFrom || null, validTo: form.validTo || null };
      return selectedId ? updateDiscountOffer(selectedId, payload) : createDiscountOffer(payload);
    },
    onSuccess: (saved) => {
      invalidate();
      setSelectedId(saved.id);
      succeed(selectedId ? `${saved.name} updated.` : `${saved.name} created.`);
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteDiscountOffer(selectedId!),
    onSuccess: () => {
      invalidate();
      succeed('Offer deleted.');
      startNew();
    },
    onError: fail,
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setDiscountOfferActive(id, isActive),
    onSuccess: invalidate,
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div className="chip">{(offersQuery.data ?? []).length} Offers</div>
        <button className="btn btn-primary" onClick={startNew}>
          + New Offer
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th style={{ width: 26 }}></th>
                <th>Offer</th>
                <th>Value</th>
                <th>Valid</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {(offersQuery.data ?? []).map((o) => (
                <tr key={o.id} className={`row-clickable${o.id === selectedId ? ' row-selected' : ''}`} onClick={() => setSelectedId(o.id)}>
                  <td onClick={(e) => e.stopPropagation()}>
                    <Switch on={o.isActive} onToggle={() => toggleActiveMutation.mutate({ id: o.id, isActive: !o.isActive })} />
                  </td>
                  <td style={{ color: 'var(--text)' }}>{o.name}</td>
                  <td>{o.discountType === 'Percent' ? `${o.value}%` : `Rs. ${o.value.toFixed(2)}`}</td>
                  <td>
                    {o.validFrom || o.validTo ? `${formatDate(o.validFrom)} – ${formatDate(o.validTo)}` : 'Always'}
                  </td>
                  <td>
                    <span className={`badge ${o.isActive ? 'badge-success' : 'badge-neutral'}`}>{o.isActive ? 'Active' : 'Inactive'}</span>
                  </td>
                </tr>
              ))}
              {(offersQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No offers yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="form-card">
          <div className="form-head">
            <div className="form-card-title">{selectedId ? 'Edit Offer' : 'New Offer'}</div>
            {selectedId && (
              <button className="close-x" onClick={startNew}>
                ✕
              </button>
            )}
          </div>
          <div className="field">
            <label>Offer Name</label>
            <input className="input" value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Weekend 10% Off" />
          </div>
          <div className="frow">
            <div className="field">
              <label>Type</label>
              <div className="type-seg">
                <button className={`type-seg-btn${form.discountType === 'Percent' ? ' on' : ''}`} onClick={() => setForm((f) => ({ ...f, discountType: 'Percent' }))}>
                  Percent
                </button>
                <button className={`type-seg-btn${form.discountType === 'Amount' ? ' on' : ''}`} onClick={() => setForm((f) => ({ ...f, discountType: 'Amount' }))}>
                  Flat Amount
                </button>
              </div>
            </div>
            <div className="field">
              <label>Value</label>
              <input className="input" type="number" step="0.01" value={form.value} onChange={(e) => setForm((f) => ({ ...f, value: Number(e.target.value) }))} />
            </div>
          </div>
          <div className="frow">
            <div className="field">
              <label>Valid From</label>
              <input className="input" type="date" value={form.validFrom ?? ''} onChange={(e) => setForm((f) => ({ ...f, validFrom: e.target.value || null }))} />
            </div>
            <div className="field">
              <label>Valid To</label>
              <input className="input" type="date" value={form.validTo ?? ''} onChange={(e) => setForm((f) => ({ ...f, validTo: e.target.value || null }))} />
            </div>
          </div>
          <div className="page-sub">Leave both dates blank for an offer that's always valid while active.</div>
          <div className="form-foot">
            <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? <span className="spinner" /> : selectedId ? 'Save Changes' : 'Create Offer'}
            </button>
            {selectedId && (
              <button
                className="btn btn-danger"
                onClick={() => {
                  if (window.confirm(`Delete "${form.name}"?`)) deleteMutation.mutate();
                }}
              >
                Delete
              </button>
            )}
          </div>
        </div>
      </div>
    </>
  );
}
