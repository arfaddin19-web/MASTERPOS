import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/AuthContext';
import { createUser, listRoles, listUsers, resetPassword, setUserActive, updateUser } from '../../api/auth-admin';
import type { UserDto } from '../../api/types';
import { Banner, Switch, useBanner } from '../../components/Shared';
import { formatDateTime } from '../../lib/format';

interface FormState {
  fullName: string;
  email: string;
  username: string;
  password: string;
  roleId: string;
}
function blank(): FormState {
  return { fullName: '', email: '', username: '', password: '', roleId: '' };
}
function toForm(u: UserDto): FormState {
  return { fullName: u.fullName, email: u.email ?? '', username: u.username, password: '', roleId: u.roleId };
}

export function UsersTab() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { banner, fail, succeed, clear } = useBanner();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(blank());
  const [newPassword, setNewPassword] = useState('');

  const usersQuery = useQuery({ queryKey: ['users'], queryFn: () => listUsers(false) });
  const rolesQuery = useQuery({ queryKey: ['roles'], queryFn: listRoles });
  const selected = (usersQuery.data ?? []).find((u) => u.id === selectedId) ?? null;

  useEffect(() => {
    if (selected) setForm(toForm(selected));
  }, [selectedId]); // eslint-disable-line react-hooks/exhaustive-deps

  function startNew() {
    setSelectedId(null);
    setForm(blank());
    clear();
  }

  const saveMutation = useMutation({
    mutationFn: () => {
      if (!form.fullName.trim() || !form.roleId) throw new Error('Full name and role are required.');
      if (selectedId) return updateUser(selectedId, { fullName: form.fullName.trim(), email: form.email.trim() || null, roleId: form.roleId, defaultBranchId: session?.defaultBranchId });
      if (!form.username.trim() || !form.password) throw new Error('Username and password are required for a new user.');
      return createUser({
        fullName: form.fullName.trim(),
        email: form.email.trim() || null,
        username: form.username.trim(),
        password: form.password,
        roleId: form.roleId,
        defaultBranchId: session?.defaultBranchId,
      });
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setSelectedId(saved.id);
      succeed(selectedId ? `${saved.fullName} updated.` : `${saved.fullName} created.`);
    },
    onError: fail,
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setUserActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
    onError: fail,
  });

  const resetPasswordMutation = useMutation({
    mutationFn: () => {
      if (!newPassword) throw new Error('Enter a new password.');
      return resetPassword(selectedId!, newPassword);
    },
    onSuccess: () => {
      succeed('Password reset.');
      setNewPassword('');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div className="chip">{(usersQuery.data ?? []).length} Users</div>
        <button className="btn btn-primary" onClick={startNew}>
          + New User
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th style={{ width: 26 }}></th>
                <th>User</th>
                <th>Role</th>
                <th>Status</th>
                <th>Last Login</th>
              </tr>
            </thead>
            <tbody>
              {(usersQuery.data ?? []).map((u) => (
                <tr key={u.id} className={`row-clickable${u.id === selectedId ? ' row-selected' : ''}`} onClick={() => setSelectedId(u.id)}>
                  <td onClick={(e) => e.stopPropagation()}>
                    <Switch
                      on={u.isActive}
                      disabled={u.id === session?.userId}
                      title={u.id === session?.userId ? "You can't deactivate your own account" : undefined}
                      onToggle={() => toggleActiveMutation.mutate({ id: u.id, isActive: !u.isActive })}
                    />
                  </td>
                  <td>
                    <div className="emp">
                      <div className="avatar-sm">{u.fullName.slice(0, 2).toUpperCase()}</div>
                      {u.fullName}
                    </div>
                  </td>
                  <td>
                    <div className="role-chip">
                      <span className="role-dot" style={{ background: 'var(--gold)' }} />
                      {u.roleName}
                    </div>
                  </td>
                  <td>
                    <span className={`badge ${u.isActive ? 'badge-success' : 'badge-neutral'}`}>{u.isActive ? 'Active' : 'Inactive'}</span>
                  </td>
                  <td>{u.lastLoginAtUtc ? formatDateTime(u.lastLoginAtUtc) : 'Never'}</td>
                </tr>
              ))}
              {(usersQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No users yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="form-card">
          <div className="form-head">
            <div className="form-card-title">{selectedId ? 'Edit User' : 'New User'}</div>
            {selectedId && (
              <button className="close-x" onClick={startNew}>
                ✕
              </button>
            )}
          </div>
          <div className="field">
            <label>Full Name</label>
            <input className="input" value={form.fullName} onChange={(e) => setForm((f) => ({ ...f, fullName: e.target.value }))} />
          </div>
          <div className="field">
            <label>Email</label>
            <input className="input" value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} />
          </div>
          <div className="field">
            <label>Role</label>
            <select className="input" value={form.roleId} onChange={(e) => setForm((f) => ({ ...f, roleId: e.target.value }))}>
              <option value="">Select…</option>
              {(rolesQuery.data ?? []).map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
          </div>
          {!selectedId && (
            <>
              <div className="field">
                <label>Username</label>
                <input className="input" value={form.username} onChange={(e) => setForm((f) => ({ ...f, username: e.target.value }))} />
              </div>
              <div className="field">
                <label>Password</label>
                <input className="input" type="password" value={form.password} onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))} />
              </div>
            </>
          )}
          <button className="btn btn-primary btn-block" disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
            {saveMutation.isPending ? <span className="spinner" /> : selectedId ? 'Save Changes' : 'Create User'}
          </button>

          {selectedId && (
            <div className="card" style={{ padding: 14 }}>
              <div className="page-sub" style={{ marginBottom: 8 }}>Reset password</div>
              <div className="field-row">
                <input className="input" type="password" placeholder="New password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} />
                <button className="btn btn-ghost" disabled={resetPasswordMutation.isPending} onClick={() => resetPasswordMutation.mutate()}>
                  Reset
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
