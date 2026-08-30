import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listParties } from '../../api/masters';
import { createOpeningBalance, deleteOpeningBalance, listAccounts, listOpeningBalances } from '../../api/accounting';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, formatRs, todayIso } from '../../lib/format';

export function OpeningBalanceTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [target, setTarget] = useState<'Party' | 'Account'>('Party');
  const [partyId, setPartyId] = useState('');
  const [accountId, setAccountId] = useState('');
  const [amount, setAmount] = useState('');
  const [balanceType, setBalanceType] = useState<'Dr' | 'Cr'>('Dr');
  const [asOfDate, setAsOfDate] = useState(todayIso());

  const partiesQuery = useQuery({ queryKey: ['parties-all'], queryFn: () => listParties({ activeOnly: true }) });
  const accountsQuery = useQuery({ queryKey: ['chart-of-accounts'], queryFn: listAccounts });
  const balancesQuery = useQuery({ queryKey: ['opening-balances'], queryFn: listOpeningBalances });

  const createMutation = useMutation({
    mutationFn: () => {
      const amt = Number(amount);
      if (!amt || amt <= 0) throw new Error('Enter a positive amount.');
      if (target === 'Party' && !partyId) throw new Error('Select a party.');
      if (target === 'Account' && !accountId) throw new Error('Select an account.');
      return createOpeningBalance({
        partyId: target === 'Party' ? partyId : null,
        accountId: target === 'Account' ? accountId : null,
        amount: amt,
        balanceType,
        asOfDate,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['opening-balances'] });
      succeed('Opening balance recorded.');
      setAmount('');
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteOpeningBalance(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['opening-balances'] });
      succeed('Opening balance removed.');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Opening Balances</div>
            <span className="chip">{(balancesQuery.data ?? []).length} recorded</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>As Of</th>
                  <th>Party / Account</th>
                  <th style={{ textAlign: 'right' }}>Amount</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {(balancesQuery.data ?? []).map((b) => (
                  <tr key={b.id}>
                    <td>{formatDate(b.asOfDate)}</td>
                    <td style={{ color: 'var(--text)' }}>{b.partyName ?? b.accountName}</td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(b.amount)} {b.balanceType}
                    </td>
                    <td>
                      <button className="close-x" onClick={() => deleteMutation.mutate(b.id)}>
                        ✕
                      </button>
                    </td>
                  </tr>
                ))}
                {(balancesQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={4} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No opening balances recorded yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">New Opening Balance</div>
          <div className="field">
            <label>Target</label>
            <div className="type-seg">
              <button className={`type-seg-btn${target === 'Party' ? ' on' : ''}`} onClick={() => setTarget('Party')}>
                Party
              </button>
              <button className={`type-seg-btn${target === 'Account' ? ' on' : ''}`} onClick={() => setTarget('Account')}>
                Account
              </button>
            </div>
          </div>
          {target === 'Party' ? (
            <div className="field">
              <label>Party</label>
              <select className="input" value={partyId} onChange={(e) => setPartyId(e.target.value)}>
                <option value="">Select…</option>
                {(partiesQuery.data ?? []).map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <div className="field">
              <label>Account</label>
              <select className="input" value={accountId} onChange={(e) => setAccountId(e.target.value)}>
                <option value="">Select…</option>
                {(accountsQuery.data ?? []).map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <div className="frow">
            <div className="field">
              <label>Amount</label>
              <input className="input" type="number" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
            </div>
            <div className="field">
              <label>Balance Type</label>
              <select className="input" value={balanceType} onChange={(e) => setBalanceType(e.target.value as 'Dr' | 'Cr')}>
                <option value="Dr">Debit (Dr)</option>
                <option value="Cr">Credit (Cr)</option>
              </select>
            </div>
          </div>
          <div className="field">
            <label>As Of Date</label>
            <input className="input" type="date" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} />
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Record Opening Balance'}
          </button>
        </div>
      </div>
    </>
  );
}
