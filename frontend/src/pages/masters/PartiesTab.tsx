import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createParty, deleteParty, listParties, setPartyActive, updateParty, type UpsertPartyRequest } from '../../api/masters';
import type { PartyDto, PartyType } from '../../api/types';
import { Banner, Switch, useBanner } from '../../components/Shared';

const PARTY_TYPES: PartyType[] = ['Supplier', 'Customer', 'Both'];

function blank(): UpsertPartyRequest {
  return { partyType: 'Customer', name: '', phone: '', email: '', address: '', vatOrPanNumber: '', openingBalanceAmount: 0, openingBalanceType: 'Dr' };
}
function toForm(p: PartyDto): UpsertPartyRequest {
  return {
    partyType: p.partyType,
    name: p.name,
    phone: p.phone ?? '',
    email: p.email ?? '',
    address: p.address ?? '',
    vatOrPanNumber: p.vatOrPanNumber ?? '',
    openingBalanceAmount: p.openingBalanceAmount,
    openingBalanceType: p.openingBalanceType,
  };
}

export function PartiesTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [filterType, setFilterType] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<UpsertPartyRequest>(blank());

  const partiesQuery = useQuery({
    queryKey: ['parties', filterType],
    queryFn: () => listParties({ partyType: filterType || undefined, activeOnly: false }),
  });
  const selected = (partiesQuery.data ?? []).find((p) => p.id === selectedId) ?? null;

  useEffect(() => {
    if (selected) setForm(toForm(selected));
  }, [selectedId]); // eslint-disable-line react-hooks/exhaustive-deps

  function startNew() {
    setSelectedId(null);
    setForm(blank());
    clear();
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!form.name.trim()) throw new Error('Name is required.');
      const payload: UpsertPartyRequest = {
        ...form,
        name: form.name.trim(),
        phone: form.phone?.trim() || null,
        email: form.email?.trim() || null,
        address: form.address?.trim() || null,
        vatOrPanNumber: form.vatOrPanNumber?.trim() || null,
        openingBalanceAmount: Number(form.openingBalanceAmount) || 0,
      };
      return selectedId ? updateParty(selectedId, payload) : createParty(payload);
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['parties'] });
      setSelectedId(saved.id);
      succeed(selectedId ? `${saved.name} updated.` : `${saved.name} created.`);
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteParty(selectedId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['parties'] });
      succeed('Party deleted.');
      startNew();
    },
    onError: fail,
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setPartyActive(id, isActive),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ['parties'] });
      if (updated.id === selectedId) setForm((f) => ({ ...f }));
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div style={{ display: 'flex', gap: 10 }}>
          <select className="input" style={{ width: 160 }} value={filterType} onChange={(e) => setFilterType(e.target.value)}>
            <option value="">All Parties</option>
            {PARTY_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
          <div className="chip">{(partiesQuery.data ?? []).length} Parties</div>
        </div>
        <button className="btn btn-primary" onClick={startNew}>
          + New Party
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th style={{ width: 26 }}></th>
                <th>Name</th>
                <th>Type</th>
                <th>Phone</th>
                <th style={{ textAlign: 'right' }}>Opening Bal.</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {(partiesQuery.data ?? []).map((p) => (
                <tr key={p.id} className={`row-clickable${p.id === selectedId ? ' row-selected' : ''}`} onClick={() => setSelectedId(p.id)}>
                  <td onClick={(e) => e.stopPropagation()}>
                    <Switch on={p.isActive} onToggle={() => toggleActiveMutation.mutate({ id: p.id, isActive: !p.isActive })} />
                  </td>
                  <td style={{ color: 'var(--text)' }}>{p.name}</td>
                  <td>{p.partyType}</td>
                  <td>{p.phone ?? '—'}</td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    Rs. {p.openingBalanceAmount.toFixed(2)} {p.openingBalanceType}
                  </td>
                  <td>
                    <span className={`badge ${p.isActive ? 'badge-success' : 'badge-neutral'}`}>{p.isActive ? 'Active' : 'Inactive'}</span>
                  </td>
                </tr>
              ))}
              {(partiesQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={6} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No parties yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="form-card">
          <div className="form-head">
            <div className="form-card-title">{selectedId ? 'Edit Party' : 'New Party'}</div>
            {selectedId && (
              <button className="close-x" onClick={startNew}>
                ✕
              </button>
            )}
          </div>
          <div className="field">
            <label>Name</label>
            <input className="input" value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
          </div>
          <div className="field">
            <label>Party Type</label>
            <div className="type-seg">
              {PARTY_TYPES.map((t) => (
                <button key={t} className={`type-seg-btn${form.partyType === t ? ' on' : ''}`} onClick={() => setForm((f) => ({ ...f, partyType: t }))}>
                  {t}
                </button>
              ))}
            </div>
          </div>
          <div className="frow">
            <div className="field">
              <label>Phone</label>
              <input className="input" value={form.phone ?? ''} onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))} />
            </div>
            <div className="field">
              <label>Email</label>
              <input className="input" value={form.email ?? ''} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} />
            </div>
          </div>
          <div className="field">
            <label>Address</label>
            <input className="input" value={form.address ?? ''} onChange={(e) => setForm((f) => ({ ...f, address: e.target.value }))} />
          </div>
          <div className="field">
            <label>VAT / PAN Number</label>
            <input className="input" value={form.vatOrPanNumber ?? ''} onChange={(e) => setForm((f) => ({ ...f, vatOrPanNumber: e.target.value }))} />
          </div>
          <div className="frow">
            <div className="field">
              <label>Opening Balance</label>
              <input
                className="input"
                type="number"
                step="0.01"
                value={form.openingBalanceAmount}
                onChange={(e) => setForm((f) => ({ ...f, openingBalanceAmount: Number(e.target.value) }))}
              />
            </div>
            <div className="field">
              <label>Balance Type</label>
              <select className="input" value={form.openingBalanceType} onChange={(e) => setForm((f) => ({ ...f, openingBalanceType: e.target.value as 'Dr' | 'Cr' }))}>
                <option value="Dr">Debit (Dr)</option>
                <option value="Cr">Credit (Cr)</option>
              </select>
            </div>
          </div>
          <div className="form-foot">
            <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? <span className="spinner" /> : selectedId ? 'Save Changes' : 'Create Party'}
            </button>
            {selectedId && (
              <button
                className="btn btn-danger"
                onClick={() => {
                  if (window.confirm(`Delete "${form.name}"? This only works if it has no transaction history.`)) deleteMutation.mutate();
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
