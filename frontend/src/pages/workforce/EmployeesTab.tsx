import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/AuthContext';
import { createEmployee, deleteEmployee, listEmployees, setEmployeeActive, updateEmployee } from '../../api/workforce';
import type { EmployeeDto, MaritalStatus } from '../../api/types';
import { Banner, Switch, useBanner } from '../../components/Shared';
import { formatRs, todayIso } from '../../lib/format';

interface FormState {
  fullName: string;
  roleTitle: string;
  phone: string;
  joinDate: string;
  basicSalary: string;
  shiftStart: string;
  shiftEnd: string;
  maritalStatus: MaritalStatus;
}
function blank(): FormState {
  return { fullName: '', roleTitle: '', phone: '', joinDate: todayIso(), basicSalary: '0', shiftStart: '', shiftEnd: '', maritalStatus: 'Single' };
}
function toForm(e: EmployeeDto): FormState {
  return {
    fullName: e.fullName,
    roleTitle: e.roleTitle ?? '',
    phone: e.phone ?? '',
    joinDate: e.joinDate,
    basicSalary: String(e.basicSalary),
    shiftStart: e.shiftStart ?? '',
    shiftEnd: e.shiftEnd ?? '',
    maritalStatus: e.maritalStatus,
  };
}

export function EmployeesTab() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { banner, fail, succeed, clear } = useBanner();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(blank());

  const employeesQuery = useQuery({ queryKey: ['employees'], queryFn: () => listEmployees(false) });
  const selected = (employeesQuery.data ?? []).find((e) => e.id === selectedId) ?? null;

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
      if (!form.fullName.trim()) throw new Error('Full name is required.');
      const basicSalary = Number(form.basicSalary) || 0;
      if (selectedId) {
        return updateEmployee(selectedId, {
          fullName: form.fullName.trim(),
          roleTitle: form.roleTitle.trim() || null,
          phone: form.phone.trim() || null,
          basicSalary,
          shiftStart: form.shiftStart || null,
          shiftEnd: form.shiftEnd || null,
          maritalStatus: form.maritalStatus,
        });
      }
      if (!session?.defaultBranchId) throw new Error('No branch on this session.');
      return createEmployee({
        branchId: session.defaultBranchId,
        fullName: form.fullName.trim(),
        roleTitle: form.roleTitle.trim() || null,
        phone: form.phone.trim() || null,
        joinDate: form.joinDate,
        basicSalary,
        shiftStart: form.shiftStart || null,
        shiftEnd: form.shiftEnd || null,
        maritalStatus: form.maritalStatus,
      });
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['employees'] });
      setSelectedId(saved.id);
      succeed(selectedId ? `${saved.fullName} updated.` : `${saved.fullName} added.`);
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteEmployee(selectedId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employees'] });
      succeed('Employee deleted.');
      startNew();
    },
    onError: fail,
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setEmployeeActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employees'] }),
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div className="chip">{(employeesQuery.data ?? []).length} Employees</div>
        <button className="btn btn-primary" onClick={startNew}>
          + New Employee
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          <table>
            <thead>
              <tr>
                <th style={{ width: 26 }}></th>
                <th>Employee</th>
                <th>Role</th>
                <th style={{ textAlign: 'right' }}>Basic Salary</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {(employeesQuery.data ?? []).map((e) => (
                <tr key={e.id} className={`row-clickable${e.id === selectedId ? ' row-selected' : ''}`} onClick={() => setSelectedId(e.id)}>
                  <td onClick={(ev) => ev.stopPropagation()}>
                    <Switch on={e.isActive} onToggle={() => toggleActiveMutation.mutate({ id: e.id, isActive: !e.isActive })} />
                  </td>
                  <td>
                    <div className="emp">
                      <div className="avatar-sm">{e.fullName.slice(0, 2).toUpperCase()}</div>
                      {e.fullName}
                    </div>
                  </td>
                  <td>{e.roleTitle ?? '—'}</td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    {formatRs(e.basicSalary)}
                  </td>
                  <td>
                    <span className={`badge ${e.isActive ? 'badge-success' : 'badge-neutral'}`}>{e.isActive ? 'Active' : 'Inactive'}</span>
                  </td>
                </tr>
              ))}
              {(employeesQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No employees yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="form-card">
          <div className="form-head">
            <div className="form-card-title">{selectedId ? 'Edit Employee' : 'New Employee'}</div>
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
          <div className="frow">
            <div className="field">
              <label>Role Title</label>
              <input className="input" value={form.roleTitle} onChange={(e) => setForm((f) => ({ ...f, roleTitle: e.target.value }))} placeholder="e.g. Cashier" />
            </div>
            <div className="field">
              <label>Phone</label>
              <input className="input" value={form.phone} onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))} />
            </div>
          </div>
          <div className="frow">
            <div className="field">
              <label>Basic Salary</label>
              <input className="input" type="number" step="0.01" value={form.basicSalary} onChange={(e) => setForm((f) => ({ ...f, basicSalary: e.target.value }))} />
            </div>
            <div className="field">
              <label>Marital Status</label>
              <select className="input" value={form.maritalStatus} onChange={(e) => setForm((f) => ({ ...f, maritalStatus: e.target.value as MaritalStatus }))}>
                <option value="Single">Single</option>
                <option value="Couple">Couple</option>
              </select>
            </div>
          </div>
          <div className="frow">
            <div className="field">
              <label>Shift Start</label>
              <input className="input" type="time" value={form.shiftStart} onChange={(e) => setForm((f) => ({ ...f, shiftStart: e.target.value }))} />
            </div>
            <div className="field">
              <label>Shift End</label>
              <input className="input" type="time" value={form.shiftEnd} onChange={(e) => setForm((f) => ({ ...f, shiftEnd: e.target.value }))} />
            </div>
          </div>
          {!selectedId && (
            <div className="field">
              <label>Join Date</label>
              <input className="input" type="date" value={form.joinDate} onChange={(e) => setForm((f) => ({ ...f, joinDate: e.target.value }))} />
            </div>
          )}
          <div className="form-foot">
            <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? <span className="spinner" /> : selectedId ? 'Save Changes' : 'Add Employee'}
            </button>
            {selectedId && (
              <button
                className="btn btn-danger"
                onClick={() => {
                  if (window.confirm(`Delete "${form.fullName}"? This only works if they have no attendance/leave/advance/payroll history.`)) deleteMutation.mutate();
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
