import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addJournalLine,
  cancelJournalEntry,
  createJournalEntry,
  getJournalEntry,
  listAccounts,
  listJournalEntries,
  postJournalEntry,
  removeJournalLine,
} from '../../api/accounting';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, formatRs, todayIso } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Draft: 'badge-gold', Posted: 'badge-success', Cancelled: 'badge-neutral' };

export function JournalEntryTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [showNew, setShowNew] = useState(false);
  const [entryDate, setEntryDate] = useState(todayIso());
  const [narration, setNarration] = useState('');

  const [lineAccountId, setLineAccountId] = useState('');
  const [lineSide, setLineSide] = useState<'Debit' | 'Credit'>('Debit');
  const [lineAmount, setLineAmount] = useState('');
  const [lineNarration, setLineNarration] = useState('');

  const accountsQuery = useQuery({ queryKey: ['chart-of-accounts'], queryFn: listAccounts });
  const entriesQuery = useQuery({ queryKey: ['journal-entries'], queryFn: () => listJournalEntries() });
  const entryQuery = useQuery({ queryKey: ['journal-entry', selectedId], queryFn: () => getJournalEntry(selectedId!), enabled: !!selectedId });

  function invalidateList() {
    queryClient.invalidateQueries({ queryKey: ['journal-entries'] });
  }
  function refreshDetail(id: string) {
    queryClient.invalidateQueries({ queryKey: ['journal-entry', id] });
    invalidateList();
  }

  const createMutation = useMutation({
    mutationFn: () => createJournalEntry({ entryDate, narration: narration.trim() || null }),
    onSuccess: (created) => {
      invalidateList();
      setSelectedId(created.id);
      setShowNew(false);
      succeed(`Draft ${created.journalNumber} created — add balanced debit/credit lines below.`);
    },
    onError: fail,
  });

  const addLineMutation = useMutation({
    mutationFn: () => {
      if (!selectedId) throw new Error('No entry selected.');
      if (!lineAccountId) throw new Error('Select an account.');
      const amount = Number(lineAmount);
      if (!amount || amount <= 0) throw new Error('Enter a positive amount.');
      return addJournalLine(selectedId, {
        accountId: lineAccountId,
        debitAmount: lineSide === 'Debit' ? amount : 0,
        creditAmount: lineSide === 'Credit' ? amount : 0,
        lineNarration: lineNarration.trim() || null,
      });
    },
    onSuccess: () => {
      refreshDetail(selectedId!);
      setLineAmount('');
      setLineNarration('');
    },
    onError: fail,
  });

  const removeLineMutation = useMutation({
    mutationFn: (lineId: string) => removeJournalLine(selectedId!, lineId),
    onSuccess: () => refreshDetail(selectedId!),
    onError: fail,
  });

  const postMutation = useMutation({
    mutationFn: () => postJournalEntry(selectedId!),
    onSuccess: () => {
      refreshDetail(selectedId!);
      succeed('Journal entry posted.');
    },
    onError: fail,
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelJournalEntry(selectedId!),
    onSuccess: () => {
      refreshDetail(selectedId!);
      succeed('Journal entry cancelled.');
    },
    onError: fail,
  });

  const entry = entryQuery.data ?? null;
  const balanced = entry ? entry.totalDebit === entry.totalCredit && entry.totalDebit > 0 : false;

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div className="chip">{(entriesQuery.data ?? []).length} Journal Entries</div>
        <button
          className="btn btn-primary"
          onClick={() => {
            setShowNew(true);
            setSelectedId(null);
            setEntryDate(todayIso());
            setNarration('');
          }}
        >
          + New Journal Entry
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th>Journal #</th>
                <th>Date</th>
                <th style={{ textAlign: 'right' }}>Debit</th>
                <th style={{ textAlign: 'right' }}>Credit</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {(entriesQuery.data ?? []).map((j) => (
                <tr
                  key={j.id}
                  className={`row-clickable${j.id === selectedId ? ' row-selected' : ''}`}
                  onClick={() => {
                    setSelectedId(j.id);
                    setShowNew(false);
                  }}
                >
                  <td style={{ color: 'var(--text)' }}>{j.journalNumber}</td>
                  <td>{formatDate(j.entryDate)}</td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    {formatRs(j.totalDebit)}
                  </td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    {formatRs(j.totalCredit)}
                  </td>
                  <td>
                    <span className={`badge ${STATUS_BADGE[j.status]}`}>{j.status}</span>
                  </td>
                </tr>
              ))}
              {(entriesQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No journal entries yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {showNew && (
          <div className="form-card">
            <div className="form-card-title">New Journal Entry</div>
            <div className="field">
              <label>Entry Date</label>
              <input className="input" type="date" value={entryDate} onChange={(e) => setEntryDate(e.target.value)} />
            </div>
            <div className="field">
              <label>Narration</label>
              <input className="input" value={narration} onChange={(e) => setNarration(e.target.value)} />
            </div>
            <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
              {createMutation.isPending ? <span className="spinner" /> : 'Create Draft'}
            </button>
          </div>
        )}

        {!showNew && entry && (
          <div className="form-card" style={{ gap: 20 }}>
            <div className="form-head">
              <div className="form-card-title">
                {entry.journalNumber} <span className={`badge ${STATUS_BADGE[entry.status]}`} style={{ marginLeft: 8 }}>{entry.status}</span>
              </div>
              <button className="close-x" onClick={() => setSelectedId(null)}>
                ✕
              </button>
            </div>
            <div className="page-sub">
              {formatDate(entry.entryDate)} {entry.narration ? `· ${entry.narration}` : ''}
            </div>

            <div className="scroll-x">
              <table className="doc-lines-table">
                <thead>
                  <tr>
                    <th>Account</th>
                    <th style={{ textAlign: 'right' }}>Debit</th>
                    <th style={{ textAlign: 'right' }}>Credit</th>
                    <th>Narration</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {entry.lines.map((l) => (
                    <tr key={l.id}>
                      <td style={{ color: 'var(--text)' }}>{l.accountName}</td>
                      <td style={{ textAlign: 'right' }} className="tabular">
                        {l.debitAmount > 0 ? formatRs(l.debitAmount) : '—'}
                      </td>
                      <td style={{ textAlign: 'right' }} className="tabular">
                        {l.creditAmount > 0 ? formatRs(l.creditAmount) : '—'}
                      </td>
                      <td>{l.lineNarration ?? '—'}</td>
                      <td>
                        {entry.status === 'Draft' && (
                          <button className="close-x" onClick={() => removeLineMutation.mutate(l.id)}>
                            ✕
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {entry.status === 'Draft' && (
              <div className="card" style={{ padding: 14 }}>
                <div className="frow" style={{ marginBottom: 10 }}>
                  <div className="field" style={{ marginBottom: 0 }}>
                    <label>Account</label>
                    <select className="input" value={lineAccountId} onChange={(e) => setLineAccountId(e.target.value)}>
                      <option value="">Select…</option>
                      {(accountsQuery.data ?? []).map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="field" style={{ marginBottom: 0 }}>
                    <label>Side</label>
                    <div className="type-seg">
                      <button className={`type-seg-btn${lineSide === 'Debit' ? ' on' : ''}`} onClick={() => setLineSide('Debit')}>
                        Debit
                      </button>
                      <button className={`type-seg-btn${lineSide === 'Credit' ? ' on' : ''}`} onClick={() => setLineSide('Credit')}>
                        Credit
                      </button>
                    </div>
                  </div>
                </div>
                <div className="frow">
                  <div className="field" style={{ marginBottom: 0 }}>
                    <label>Amount</label>
                    <input className="input" type="number" step="0.01" value={lineAmount} onChange={(e) => setLineAmount(e.target.value)} />
                  </div>
                  <div className="field" style={{ marginBottom: 0 }}>
                    <label>Line Narration</label>
                    <input className="input" value={lineNarration} onChange={(e) => setLineNarration(e.target.value)} />
                  </div>
                </div>
                <button className="btn btn-ghost btn-block" style={{ marginTop: 10 }} disabled={addLineMutation.isPending} onClick={() => addLineMutation.mutate()}>
                  + Add Line
                </button>
              </div>
            )}

            <div className="summary" style={{ padding: 0, border: 'none' }}>
              <div className="srow">
                <span>Total Debit</span>
                <span className="tabular">{formatRs(entry.totalDebit)}</span>
              </div>
              <div className="srow total">
                <span>Total Credit</span>
                <span className="val tabular" style={{ color: balanced ? 'var(--success)' : 'var(--danger)' }}>
                  {formatRs(entry.totalCredit)}
                </span>
              </div>
              {!balanced && entry.lines.length > 0 && <div className="error-text">Debits and credits must match before posting.</div>}
            </div>

            <div className="form-foot">
              {entry.status === 'Draft' && (
                <>
                  <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={postMutation.isPending || !balanced} onClick={() => postMutation.mutate()}>
                    {postMutation.isPending ? <span className="spinner" /> : 'Post Entry'}
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
