import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/AuthContext';
import { createTable, deleteTable, listTables, updateTable } from '../../api/masters';
import type { DiningTableDto } from '../../api/types';
import { Banner, useBanner } from '../../components/Shared';

interface FormState {
  tableNumber: string;
  floorLabel: string;
  seats: string;
}
function blank(): FormState {
  return { tableNumber: '', floorLabel: '', seats: '4' };
}
function toForm(t: DiningTableDto): FormState {
  return { tableNumber: t.tableNumber, floorLabel: t.floorLabel ?? '', seats: String(t.seats) };
}

const STATUS_BADGE: Record<string, string> = { Vacant: 'badge-success', Occupied: 'badge-danger', PartiallyPaid: 'badge-gold' };

export function TablesTab() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { banner, fail, succeed, clear } = useBanner();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(blank());

  const tablesQuery = useQuery({ queryKey: ['masters-tables'], queryFn: () => listTables() });
  const selected = (tablesQuery.data ?? []).find((t) => t.id === selectedId) ?? null;

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
      if (!form.tableNumber.trim()) throw new Error('Table number is required.');
      const seats = Number(form.seats) || 1;
      if (selectedId) return updateTable(selectedId, { tableNumber: form.tableNumber.trim(), floorLabel: form.floorLabel.trim() || null, seats });
      if (!session?.defaultBranchId) throw new Error('No branch on this session.');
      return createTable({ branchId: session.defaultBranchId, tableNumber: form.tableNumber.trim(), floorLabel: form.floorLabel.trim() || null, seats });
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['masters-tables'] });
      queryClient.invalidateQueries({ queryKey: ['tables'] });
      setSelectedId(saved.id);
      succeed(selectedId ? `Table ${saved.tableNumber} updated.` : `Table ${saved.tableNumber} created.`);
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteTable(selectedId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masters-tables'] });
      queryClient.invalidateQueries({ queryKey: ['tables'] });
      succeed('Table deleted.');
      startNew();
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div className="chip">{(tablesQuery.data ?? []).length} Tables</div>
        <button className="btn btn-primary" onClick={startNew}>
          + New Table
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th>Table</th>
                <th>Floor</th>
                <th style={{ textAlign: 'right' }}>Seats</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {(tablesQuery.data ?? []).map((t) => (
                <tr key={t.id} className={`row-clickable${t.id === selectedId ? ' row-selected' : ''}`} onClick={() => setSelectedId(t.id)}>
                  <td style={{ color: 'var(--text)' }}>{t.tableNumber}</td>
                  <td>{t.floorLabel ?? '—'}</td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    {t.seats}
                  </td>
                  <td>
                    <span className={`badge ${STATUS_BADGE[t.status] ?? 'badge-neutral'}`}>{t.status}</span>
                  </td>
                </tr>
              ))}
              {(tablesQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={4} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No tables yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="form-card">
          <div className="form-head">
            <div className="form-card-title">{selectedId ? 'Edit Table' : 'New Table'}</div>
            {selectedId && (
              <button className="close-x" onClick={startNew}>
                ✕
              </button>
            )}
          </div>
          {selected && selected.status !== 'Vacant' && (
            <div className="page-sub" style={{ color: 'var(--danger)' }}>
              This table is currently {selected.status} — edit/delete is blocked until it's vacant again.
            </div>
          )}
          <div className="field">
            <label>Table Number</label>
            <input className="input" value={form.tableNumber} onChange={(e) => setForm((f) => ({ ...f, tableNumber: e.target.value }))} />
          </div>
          <div className="frow">
            <div className="field">
              <label>Floor</label>
              <input className="input" value={form.floorLabel} onChange={(e) => setForm((f) => ({ ...f, floorLabel: e.target.value }))} placeholder="e.g. Ground Floor" />
            </div>
            <div className="field">
              <label>Seats</label>
              <input className="input" type="number" value={form.seats} onChange={(e) => setForm((f) => ({ ...f, seats: e.target.value }))} />
            </div>
          </div>
          <div className="form-foot">
            <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? <span className="spinner" /> : selectedId ? 'Save Changes' : 'Create Table'}
            </button>
            {selectedId && (
              <button
                className="btn btn-danger"
                onClick={() => {
                  if (window.confirm(`Delete Table ${form.tableNumber}?`)) deleteMutation.mutate();
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
