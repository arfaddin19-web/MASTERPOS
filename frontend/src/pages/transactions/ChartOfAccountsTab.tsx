import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createAccount, deleteAccount, listAccounts, seedDefaultAccounts } from '../../api/accounting';
import type { AccountType } from '../../api/types';
import { Banner, useBanner } from '../../components/Shared';

const TYPES: AccountType[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'];

export function ChartOfAccountsTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [name, setName] = useState('');
  const [accountType, setAccountType] = useState<AccountType>('Asset');
  const [parentAccountId, setParentAccountId] = useState('');

  const accountsQuery = useQuery({ queryKey: ['chart-of-accounts'], queryFn: listAccounts });

  const seedMutation = useMutation({
    mutationFn: seedDefaultAccounts,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['chart-of-accounts'] });
      succeed('Default chart of accounts created.');
    },
    onError: fail,
  });

  const createMutation = useMutation({
    mutationFn: () => {
      if (!name.trim()) throw new Error('Account name is required.');
      return createAccount({ name: name.trim(), accountType, parentAccountId: parentAccountId || null });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['chart-of-accounts'] });
      succeed('Account created.');
      setName('');
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAccount(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['chart-of-accounts'] });
      succeed('Account deleted.');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div className="chip">{(accountsQuery.data ?? []).length} Accounts</div>
        {(accountsQuery.data ?? []).length === 0 && (
          <button className="btn btn-ghost" disabled={seedMutation.isPending} onClick={() => seedMutation.mutate()}>
            {seedMutation.isPending ? <span className="spinner" /> : 'Seed Default Accounts'}
          </button>
        )}
      </div>
      <div className="two-col">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th>Account</th>
                <th>Type</th>
                <th>Parent</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {(accountsQuery.data ?? []).map((a) => (
                <tr key={a.id}>
                  <td style={{ color: 'var(--text)' }}>
                    {a.name} {a.isSystemAccount && <span className="badge badge-gold" style={{ marginLeft: 6 }}>System</span>}
                  </td>
                  <td>{a.accountType}</td>
                  <td>{a.parentAccountName ?? '—'}</td>
                  <td>
                    {!a.isSystemAccount && (
                      <button
                        className="close-x"
                        onClick={() => {
                          if (window.confirm(`Delete "${a.name}"?`)) deleteMutation.mutate(a.id);
                        }}
                      >
                        ✕
                      </button>
                    )}
                  </td>
                </tr>
              ))}
              {(accountsQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={4} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No accounts yet — seed the defaults or add your own.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        <div className="form-card">
          <div className="form-card-title">New Account</div>
          <div className="field">
            <label>Name</label>
            <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="field">
            <label>Type</label>
            <select className="input" value={accountType} onChange={(e) => setAccountType(e.target.value as AccountType)}>
              {TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Parent Account</label>
            <select className="input" value={parentAccountId} onChange={(e) => setParentAccountId(e.target.value)}>
              <option value="">— None —</option>
              {(accountsQuery.data ?? []).map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </select>
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Create Account'}
          </button>
        </div>
      </div>
    </>
  );
}
