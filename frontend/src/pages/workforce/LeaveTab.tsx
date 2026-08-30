import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { approveLeave, cancelLeave, createLeaveRequest, listEmployees, listLeaveRequests, rejectLeave } from '../../api/workforce';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, todayIso } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Pending: 'badge-gold', Approved: 'badge-success', Rejected: 'badge-danger', Cancelled: 'badge-neutral' };

export function LeaveTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [employeeId, setEmployeeId] = useState('');
  const [leaveType, setLeaveType] = useState('Sick');
  const [fromDate, setFromDate] = useState(todayIso());
  const [toDate, setToDate] = useState(todayIso());
  const [reason, setReason] = useState('');

  const employeesQuery = useQuery({ queryKey: ['wf-employees-active'], queryFn: () => listEmployees(true) });
  const requestsQuery = useQuery({ queryKey: ['leave-requests'], queryFn: () => listLeaveRequests() });

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['leave-requests'] });
  }

  const createMutation = useMutation({
    mutationFn: () => {
      if (!employeeId) throw new Error('Select an employee.');
      return createLeaveRequest({ employeeId, leaveType, fromDate, toDate, reason: reason.trim() || null });
    },
    onSuccess: () => {
      invalidate();
      succeed('Leave request submitted.');
      setReason('');
    },
    onError: fail,
  });

  const approveMutation = useMutation({ mutationFn: (id: string) => approveLeave(id), onSuccess: () => { invalidate(); succeed('Leave approved.'); }, onError: fail });
  const rejectMutation = useMutation({ mutationFn: (id: string) => rejectLeave(id), onSuccess: () => { invalidate(); succeed('Leave rejected.'); }, onError: fail });
  const cancelMutation = useMutation({ mutationFn: (id: string) => cancelLeave(id), onSuccess: () => { invalidate(); succeed('Leave cancelled.'); }, onError: fail });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Leave Requests</div>
            <span className="chip">{(requestsQuery.data ?? []).length} total</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Employee</th>
                  <th>Type</th>
                  <th>Dates</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {(requestsQuery.data ?? []).map((r) => (
                  <tr key={r.id}>
                    <td style={{ color: 'var(--text)' }}>{r.employeeName}</td>
                    <td>{r.leaveType}</td>
                    <td>
                      {formatDate(r.fromDate)} – {formatDate(r.toDate)}
                    </td>
                    <td>
                      <span className={`badge ${STATUS_BADGE[r.status]}`}>{r.status}</span>
                    </td>
                    <td>
                      {r.status === 'Pending' && (
                        <div style={{ display: 'flex', gap: 6 }}>
                          <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} onClick={() => approveMutation.mutate(r.id)}>
                            Approve
                          </button>
                          <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} onClick={() => rejectMutation.mutate(r.id)}>
                            Reject
                          </button>
                          <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} onClick={() => cancelMutation.mutate(r.id)}>
                            Cancel
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
                {(requestsQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No leave requests yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">New Leave Request</div>
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
          <div className="field">
            <label>Leave Type</label>
            <select className="input" value={leaveType} onChange={(e) => setLeaveType(e.target.value)}>
              <option value="Sick">Sick</option>
              <option value="Casual">Casual</option>
              <option value="Annual">Annual</option>
              <option value="Unpaid">Unpaid</option>
            </select>
          </div>
          <div className="frow">
            <div className="field">
              <label>From</label>
              <input className="input" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
            </div>
            <div className="field">
              <label>To</label>
              <input className="input" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
            </div>
          </div>
          <div className="field">
            <label>Reason</label>
            <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Submit Request'}
          </button>
        </div>
      </div>
    </>
  );
}
