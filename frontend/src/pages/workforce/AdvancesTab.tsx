import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createAdvance, listAdvances, listEmployees, recoverAdvance } from '../../api/workforce';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, formatRs, todayIso } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Open: 'badge-gold', PartiallyRecovered: 'badge-gold', Recovered: 'badge-success' };

export function AdvancesTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [employeeId, setEmployeeId] = useState('');
  const [amount, setAmount] = useState('');
  const [advanceDate, setAdvanceDate] = useState(todayIso());
  const [reason, setReason] = useState('');
  const [recoverAmounts, setRecoverAmounts] = useState<Record<string, string>>({});

  const employeesQuery = useQuery({ queryKey: ['wf-employees-active'], queryFn: () => listEmployees(true) });
  const advancesQuery = useQuery({ queryKey: ['advances'], queryFn: () => listAdvances() });

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['advances'] });
  }

  const createMutation = useMutation({
    mutationFn: () => {
      if (!employeeId) throw new Error('Select an employee.');
      const amt = Number(amount);
      if (!amt || amt <= 0) throw new Error('Enter a positive amount.');
      return createAdvance({ employeeId, amount: amt, advanceDate, reason: reason.trim() || null });
    },
    onSuccess: () => {
      invalidate();
      succeed('Advance recorded.');
      setAmount('');
      setReason('');
    },
    onError: fail,
  });

  const recoverMutation = useMutation({
    mutationFn: ({ id, amount: amt }: { id: string; amount: number }) => recoverAdvance(id, amt),
    onSuccess: () => {
      invalidate();
      succeed('Recovery recorded.');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Employee Advances</div>
            <span className="chip">{(advancesQuery.data ?? []).length} total</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Employee</th>
                  <th>Date</th>
                  <th style={{ textAlign: 'right' }}>Amount</th>
                  <th style={{ textAlign: 'right' }}>Balance</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {(advancesQuery.data ?? []).map((a) => (
                  <tr key={a.id}>
                    <td style={{ color: 'var(--text)' }}>{a.employeeName}</td>
                    <td>{formatDate(a.advanceDate)}</td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(a.amount)}
                    </td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(a.balance)}
                    </td>
                    <td>
                      <span className={`badge ${STATUS_BADGE[a.status]}`}>{a.status}</span>
                    </td>
                    <td>
                      {a.status !== 'Recovered' && (
                        <div className="field-row" style={{ minWidth: 160 }}>
                          <input
                            className="input mini-input"
                            style={{ width: 70 }}
                            placeholder="0"
                            value={recoverAmounts[a.id] ?? ''}
                            onChange={(e) => setRecoverAmounts((m) => ({ ...m, [a.id]: e.target.value }))}
                          />
                          <button
                            className="btn btn-ghost"
                            style={{ padding: '5px 10px', fontSize: 11 }}
                            onClick={() => {
                              const amt = Number(recoverAmounts[a.id]);
                              if (!amt || amt <= 0) return fail(new Error('Enter a recovery amount.'));
                              recoverMutation.mutate({ id: a.id, amount: amt });
                            }}
                          >
                            Recover
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
                {(advancesQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={6} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No advances yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">New Advance</div>
          <div className="field">
            <label>Employee</label>
            <select className="input" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
              <option value="">Select…</option>
              {(employeesQuery.data ?? []).map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName}
                </option>
              ))}
            </select>
          </div>
          <div className="frow">
            <div className="field">
              <label>Amount</label>
              <input className="input" type="number" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
            </div>
            <div className="field">
              <label>Date</label>
              <input className="input" type="date" value={advanceDate} onChange={(e) => setAdvanceDate(e.target.value)} />
            </div>
          </div>
          <div className="field">
            <label>Reason</label>
            <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Record Advance'}
          </button>
        </div>
      </div>
    </>
  );
}
