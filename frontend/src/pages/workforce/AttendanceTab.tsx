import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { checkIn, getTodayAttendance, listAttendance, listEmployees, markAttendance } from '../../api/workforce';
import { Banner, useBanner } from '../../components/Shared';
import { formatDateTime, todayIso } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Present: 'badge-success', Late: 'badge-gold', Absent: 'badge-danger', OnLeave: 'badge-gold' };

export function AttendanceTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [markEmployeeId, setMarkEmployeeId] = useState('');
  const [markStatus, setMarkStatus] = useState('Present');
  const [markDate, setMarkDate] = useState(todayIso());
  const [markOt, setMarkOt] = useState('0');

  const employeesQuery = useQuery({ queryKey: ['wf-employees-active'], queryFn: () => listEmployees(true) });
  const todayQuery = useQuery({ queryKey: ['attendance-today'], queryFn: getTodayAttendance });
  const historyQuery = useQuery({ queryKey: ['attendance-history'], queryFn: () => listAttendance() });

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['attendance-today'] });
    queryClient.invalidateQueries({ queryKey: ['attendance-history'] });
  }

  const checkInMutation = useMutation({
    mutationFn: (employeeId: string) => checkIn(employeeId),
    onSuccess: () => {
      invalidate();
      succeed('Checked in.');
    },
    onError: fail,
  });

  const markMutation = useMutation({
    mutationFn: () => {
      if (!markEmployeeId) throw new Error('Select an employee.');
      return markAttendance({ employeeId: markEmployeeId, attendanceDate: markDate, status: markStatus, overtimeHours: Number(markOt) || 0 });
    },
    onSuccess: () => {
      invalidate();
      succeed('Attendance marked.');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="card">
        <div className="card-head">
          <div className="card-title">Today's Attendance Snapshot</div>
          <span className="chip">{todayIso()}</span>
        </div>
        <div className="scroll-x">
          <table>
            <thead>
              <tr>
                <th>Employee</th>
                <th>Shift</th>
                <th>Check-in</th>
                <th>Check-out</th>
                <th style={{ textAlign: 'right' }}>OT Hrs</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {(todayQuery.data ?? []).map((row) => (
                <tr key={row.employeeId}>
                  <td>
                    <div className="emp">
                      <div className="avatar-sm">{row.employeeName.slice(0, 2).toUpperCase()}</div>
                      {row.employeeName}
                    </div>
                  </td>
                  <td>{row.shiftStart && row.shiftEnd ? `${row.shiftStart} – ${row.shiftEnd}` : '—'}</td>
                  <td>{row.checkInAtUtc ? formatDateTime(row.checkInAtUtc) : '—'}</td>
                  <td>{row.checkOutAtUtc ? formatDateTime(row.checkOutAtUtc) : '—'}</td>
                  <td style={{ textAlign: 'right' }} className="tabular">
                    {row.overtimeHours ?? '—'}
                  </td>
                  <td>{row.status ? <span className={`badge ${STATUS_BADGE[row.status] ?? 'badge-neutral'}`}>{row.status}</span> : <span className="muted">—</span>}</td>
                  <td>
                    {!row.checkInAtUtc && (
                      <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} disabled={checkInMutation.isPending} onClick={() => checkInMutation.mutate(row.employeeId)}>
                        Check In
                      </button>
                    )}
                  </td>
                </tr>
              ))}
              {(todayQuery.data ?? []).length === 0 && (
                <tr>
                  <td colSpan={7} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No active employees.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Attendance History</div>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Employee</th>
                  <th>Status</th>
                  <th style={{ textAlign: 'right' }}>OT Hrs</th>
                </tr>
              </thead>
              <tbody>
                {(historyQuery.data ?? []).slice(0, 40).map((a) => (
                  <tr key={a.id}>
                    <td>{a.attendanceDate}</td>
                    <td style={{ color: 'var(--text)' }}>{a.employeeName}</td>
                    <td>
                      <span className={`badge ${STATUS_BADGE[a.status] ?? 'badge-neutral'}`}>{a.status}</span>
                    </td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {a.overtimeHours}
                    </td>
                  </tr>
                ))}
                {(historyQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={4} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No history yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">Manual Mark / Correction</div>
          <div className="field">
            <label>Employee</label>
            <select className="input" value={markEmployeeId} onChange={(e) => setMarkEmployeeId(e.target.value)}>
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
              <label>Date</label>
              <input className="input" type="date" value={markDate} onChange={(e) => setMarkDate(e.target.value)} />
            </div>
            <div className="field">
              <label>Status</label>
              <select className="input" value={markStatus} onChange={(e) => setMarkStatus(e.target.value)}>
                <option value="Present">Present</option>
                <option value="Late">Late</option>
                <option value="Absent">Absent</option>
                <option value="OnLeave">On Leave</option>
              </select>
            </div>
          </div>
          <div className="field">
            <label>Overtime Hours</label>
            <input className="input" type="number" step="0.25" value={markOt} onChange={(e) => setMarkOt(e.target.value)} />
          </div>
          <button className="btn btn-primary btn-block" disabled={markMutation.isPending} onClick={() => markMutation.mutate()}>
            {markMutation.isPending ? <span className="spinner" /> : 'Save Attendance'}
          </button>
        </div>
      </div>
    </>
  );
}
