import { Fragment, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/AuthContext';
import {
  completePayrollRun,
  createPayrollRun,
  createTaxSlab,
  deleteTaxSlab,
  getPayrollSettings,
  listPayrollRuns,
  listTaxSlabs,
  recomputePayrollRun,
  seedDefaultTaxSlabs,
  updatePayrollSettings,
} from '../../api/workforce';
import type { PayrollSettingsDto } from '../../api/types';
import { Banner, Switch, useBanner } from '../../components/Shared';
import { formatDateTime, formatRs } from '../../lib/format';

const STATUS_BADGE: Record<string, string> = { Draft: 'badge-gold', Completed: 'badge-success' };
const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

export function PayrollTab() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { banner, fail, succeed, clear } = useBanner();
  const now = new Date();
  const [periodMonth, setPeriodMonth] = useState(now.getMonth() + 1);
  const [periodYear, setPeriodYear] = useState(now.getFullYear());
  const [runType, setRunType] = useState<'Monthly' | 'FestivalBonus'>('Monthly');
  const [expandedRunId, setExpandedRunId] = useState<string | null>(null);
  const [slabStatus, setSlabStatus] = useState<'Single' | 'Couple'>('Single');
  const [slabLower, setSlabLower] = useState('0');
  const [slabUpper, setSlabUpper] = useState('');
  const [slabRate, setSlabRate] = useState('1');

  const [settingsForm, setSettingsForm] = useState<PayrollSettingsDto | null>(null);

  const settingsQuery = useQuery({ queryKey: ['payroll-settings'], queryFn: getPayrollSettings });
  const taxSlabsQuery = useQuery({ queryKey: ['tax-slabs'], queryFn: listTaxSlabs });
  const runsQuery = useQuery({ queryKey: ['payroll-runs'], queryFn: () => listPayrollRuns() });

  useEffect(() => {
    if (settingsQuery.data && !settingsForm) setSettingsForm(settingsQuery.data);
  }, [settingsQuery.data]); // eslint-disable-line react-hooks/exhaustive-deps

  const saveSettingsMutation = useMutation({
    mutationFn: () => updatePayrollSettings(settingsForm!),
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['payroll-settings'] });
      setSettingsForm(saved);
      succeed('Payroll settings saved.');
    },
    onError: fail,
  });

  const seedSlabsMutation = useMutation({
    mutationFn: seedDefaultTaxSlabs,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tax-slabs'] });
      succeed('Default tax slabs loaded.');
    },
    onError: fail,
  });

  const deleteSlabMutation = useMutation({
    mutationFn: (id: string) => deleteTaxSlab(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tax-slabs'] });
    },
    onError: fail,
  });

  const createSlabMutation = useMutation({
    mutationFn: () =>
      createTaxSlab({ maritalStatus: slabStatus, lowerBound: Number(slabLower) || 0, upperBound: slabUpper ? Number(slabUpper) : null, ratePercent: Number(slabRate) || 0 }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tax-slabs'] });
      succeed('Tax slab added.');
      setSlabLower(slabUpper || '0');
      setSlabUpper('');
    },
    onError: fail,
  });

  const createRunMutation = useMutation({
    mutationFn: () => {
      if (!session?.defaultBranchId) throw new Error('No branch on this session.');
      return createPayrollRun({ branchId: session.defaultBranchId, periodMonth, periodYear, runType });
    },
    onSuccess: (run) => {
      queryClient.invalidateQueries({ queryKey: ['payroll-runs'] });
      setExpandedRunId(run.id);
      succeed(`${run.runType} run for ${MONTHS[run.periodMonth - 1]} ${run.periodYear} created.`);
    },
    onError: fail,
  });

  const recomputeMutation = useMutation({
    mutationFn: (id: string) => recomputePayrollRun(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payroll-runs'] });
      succeed('Recomputed.');
    },
    onError: fail,
  });

  const completeMutation = useMutation({
    mutationFn: (id: string) => completePayrollRun(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payroll-runs'] });
      succeed('Payroll run completed — advances recovered.');
    },
    onError: fail,
  });

  const runs = runsQuery.data ?? [];
  const totalGross = runs.reduce((s, r) => s + r.grossPayroll, 0);
  const totalNet = runs.reduce((s, r) => s + r.netPayroll, 0);
  const headcount = new Set(runs.flatMap((r) => r.lines.map((l) => l.employeeId))).size;

  return (
    <>
      <Banner banner={banner} onClear={clear} />

      <div className="kpi-grid">
        <div className="card">
          <div className="kpi-label">Payroll Runs</div>
          <div className="kpi-num tabular">{runs.length}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Employees Paid</div>
          <div className="kpi-num tabular">{headcount}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Gross Payroll (all runs)</div>
          <div className="kpi-num tabular">{formatRs(totalGross)}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Net Payroll (all runs)</div>
          <div className="kpi-num tabular" style={{ color: 'var(--gold-bright)' }}>
            {formatRs(totalNet)}
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-head">
          <div>
            <div className="card-title">Run Payroll</div>
            <div className="page-sub">Computes every active employee from attendance, leave & advance records for the period.</div>
          </div>
        </div>
        <div className="header-grid" style={{ marginBottom: 14 }}>
          <div className="field">
            <label>Month</label>
            <select className="input" value={periodMonth} onChange={(e) => setPeriodMonth(Number(e.target.value))}>
              {MONTHS.map((m, i) => (
                <option key={m} value={i + 1}>
                  {m}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Year</label>
            <input className="input" type="number" value={periodYear} onChange={(e) => setPeriodYear(Number(e.target.value))} />
          </div>
          <div className="field">
            <label>Run Type</label>
            <select className="input" value={runType} onChange={(e) => setRunType(e.target.value as 'Monthly' | 'FestivalBonus')}>
              <option value="Monthly">Monthly</option>
              <option value="FestivalBonus">Festival Bonus</option>
            </select>
          </div>
          <div className="field" style={{ display: 'flex', alignItems: 'flex-end' }}>
            <button className="btn btn-primary btn-block" disabled={createRunMutation.isPending} onClick={() => createRunMutation.mutate()}>
              {createRunMutation.isPending ? <span className="spinner" /> : 'Run Payroll'}
            </button>
          </div>
        </div>

        <div className="scroll-x">
          <table>
            <thead>
              <tr>
                <th>Period</th>
                <th>Type</th>
                <th style={{ textAlign: 'right' }}>Gross</th>
                <th style={{ textAlign: 'right' }}>Net</th>
                <th>Status</th>
                <th>Run At</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {runs.map((r) => (
                <Fragment key={r.id}>
                  <tr className="row-clickable" onClick={() => setExpandedRunId(expandedRunId === r.id ? null : r.id)}>
                    <td style={{ color: 'var(--text)' }}>
                      {MONTHS[r.periodMonth - 1]} {r.periodYear}
                    </td>
                    <td>{r.runType}</td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(r.grossPayroll)}
                    </td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(r.netPayroll)}
                    </td>
                    <td>
                      <span className={`badge ${STATUS_BADGE[r.status]}`}>{r.status}</span>
                    </td>
                    <td>{formatDateTime(r.runAtUtc)}</td>
                    <td onClick={(e) => e.stopPropagation()}>
                      {r.status === 'Draft' && (
                        <div style={{ display: 'flex', gap: 6 }}>
                          <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} onClick={() => recomputeMutation.mutate(r.id)}>
                            Recompute
                          </button>
                          <button className="btn btn-ghost" style={{ padding: '5px 10px', fontSize: 11 }} onClick={() => completeMutation.mutate(r.id)}>
                            Complete
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                  {expandedRunId === r.id && (
                    <tr>
                      <td colSpan={7} style={{ padding: 0, borderTop: 'none' }}>
                        <div className="scroll-x" style={{ padding: '4px 0 14px' }}>
                          <table>
                            <thead>
                              <tr>
                                <th>Employee</th>
                                <th style={{ textAlign: 'right' }}>Basic</th>
                                <th style={{ textAlign: 'right' }}>Allow.</th>
                                <th style={{ textAlign: 'right' }}>OT</th>
                                <th style={{ textAlign: 'right' }}>PF (Ee/Er)</th>
                                <th style={{ textAlign: 'right' }}>SSF (Ee/Er)</th>
                                <th style={{ textAlign: 'right' }}>TDS</th>
                                <th style={{ textAlign: 'right' }}>Advance</th>
                                <th style={{ textAlign: 'right' }}>Net Pay</th>
                                <th>Status</th>
                              </tr>
                            </thead>
                            <tbody>
                              {r.lines.map((l) => (
                                <tr key={l.id}>
                                  <td>
                                    <div className="emp">
                                      <div className="avatar-sm">{l.employeeName.slice(0, 2).toUpperCase()}</div>
                                      {l.employeeName}
                                    </div>
                                  </td>
                                  <td style={{ textAlign: 'right' }} className="tabular">
                                    {formatRs(l.basicAmount)}
                                  </td>
                                  <td style={{ textAlign: 'right' }} className="tabular">
                                    {formatRs(l.allowancesAmount)}
                                  </td>
                                  <td style={{ textAlign: 'right' }} className="tabular">
                                    {formatRs(l.overtimeAmount)}
                                  </td>
                                  <td style={{ textAlign: 'right' }} className="tabular">
                                    {formatRs(l.pfEmployeeAmount)} / {formatRs(l.pfEmployerAmount)}
                                  </td>
                                  <td style={{ textAlign: 'right' }} className="tabular">
                                    {formatRs(l.ssfEmployeeAmount)} / {formatRs(l.ssfEmployerAmount)}
                                  </td>
                                  <td style={{ textAlign: 'right' }} className="tabular">
                                    {formatRs(l.tdsAmount)}
                                  </td>
                                  <td style={{ textAlign: 'right' }} className="tabular">
                                    {formatRs(l.advanceDeductionAmount)}
                                  </td>
                                  <td style={{ textAlign: 'right', color: 'var(--text)' }} className="tabular">
                                    {formatRs(l.netPayAmount)}
                                  </td>
                                  <td>
                                    <span className={`badge ${l.lineStatus === 'Ready' ? 'badge-success' : 'badge-gold'}`}>{l.lineStatus}</span>
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
              {runs.length === 0 && (
                <tr>
                  <td colSpan={7} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    No payroll runs yet.
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
            <div className="card-title">Payroll Settings</div>
          </div>
          {settingsForm && (
            <div className="stack">
              <div className="util-row">
                <div>
                  <div className="util-name">Overtime</div>
                  <div className="util-sub">{settingsForm.overtimeMultiplier}× rate</div>
                </div>
                <Switch on={settingsForm.overtimeEnabled} onToggle={() => setSettingsForm((f) => f && { ...f, overtimeEnabled: !f.overtimeEnabled })} />
              </div>
              <div className="util-row">
                <div>
                  <div className="util-name">Provident Fund (PF)</div>
                  <div className="util-sub">
                    {settingsForm.pfEmployeePercent}% employee / {settingsForm.pfEmployerPercent}% employer
                  </div>
                </div>
                <Switch on={settingsForm.pfEnabled} onToggle={() => setSettingsForm((f) => f && { ...f, pfEnabled: !f.pfEnabled })} />
              </div>
              <div className="util-row">
                <div>
                  <div className="util-name">Social Security Fund (SSF)</div>
                  <div className="util-sub">
                    {settingsForm.ssfEmployeePercent}% employee / {settingsForm.ssfEmployerPercent}% employer
                  </div>
                </div>
                <Switch on={settingsForm.ssfEnabled} onToggle={() => setSettingsForm((f) => f && { ...f, ssfEnabled: !f.ssfEnabled })} />
              </div>
              <div className="util-row">
                <div>
                  <div className="util-name">TDS (Income Tax)</div>
                  <div className="util-sub">Uses the Tax Slabs table</div>
                </div>
                <Switch on={settingsForm.tdsEnabled} onToggle={() => setSettingsForm((f) => f && { ...f, tdsEnabled: !f.tdsEnabled })} />
              </div>
              <div className="util-row">
                <div>
                  <div className="util-name">Festival Bonus</div>
                  <div className="util-sub">{settingsForm.festivalBonusPercent}% of basic salary</div>
                </div>
                <Switch on={settingsForm.festivalBonusEnabled} onToggle={() => setSettingsForm((f) => f && { ...f, festivalBonusEnabled: !f.festivalBonusEnabled })} />
              </div>
              <button className="btn btn-primary btn-block" disabled={saveSettingsMutation.isPending} onClick={() => saveSettingsMutation.mutate()}>
                {saveSettingsMutation.isPending ? <span className="spinner" /> : 'Save Settings'}
              </button>
            </div>
          )}
        </div>

        <div className="card">
          <div className="card-head">
            <div className="card-title">Tax Slabs (Income Tax)</div>
            {(taxSlabsQuery.data ?? []).length === 0 && (
              <button className="btn btn-ghost" disabled={seedSlabsMutation.isPending} onClick={() => seedSlabsMutation.mutate()}>
                Seed Defaults
              </button>
            )}
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Status</th>
                  <th style={{ textAlign: 'right' }}>From</th>
                  <th style={{ textAlign: 'right' }}>To</th>
                  <th style={{ textAlign: 'right' }}>Rate</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {(taxSlabsQuery.data ?? []).map((s) => (
                  <tr key={s.id}>
                    <td style={{ color: 'var(--text)' }}>{s.maritalStatus}</td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(s.lowerBound)}
                    </td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {s.upperBound != null ? formatRs(s.upperBound) : '∞'}
                    </td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {s.ratePercent}%
                    </td>
                    <td>
                      <button className="close-x" onClick={() => deleteSlabMutation.mutate(s.id)}>
                        ✕
                      </button>
                    </td>
                  </tr>
                ))}
                {(taxSlabsQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No tax slabs configured yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="frow" style={{ marginTop: 14 }}>
            <div className="field" style={{ marginBottom: 0 }}>
              <label>Status</label>
              <select className="input" value={slabStatus} onChange={(e) => setSlabStatus(e.target.value as 'Single' | 'Couple')}>
                <option value="Single">Single</option>
                <option value="Couple">Couple</option>
              </select>
            </div>
            <div className="field" style={{ marginBottom: 0 }}>
              <label>Rate %</label>
              <input className="input" type="number" step="0.01" value={slabRate} onChange={(e) => setSlabRate(e.target.value)} />
            </div>
          </div>
          <div className="frow" style={{ marginTop: 12 }}>
            <div className="field" style={{ marginBottom: 0 }}>
              <label>Lower Bound</label>
              <input className="input" type="number" step="0.01" value={slabLower} onChange={(e) => setSlabLower(e.target.value)} />
            </div>
            <div className="field" style={{ marginBottom: 0 }}>
              <label>Upper Bound (blank = top band)</label>
              <input className="input" type="number" step="0.01" value={slabUpper} onChange={(e) => setSlabUpper(e.target.value)} />
            </div>
          </div>
          <button className="btn btn-ghost btn-block" style={{ marginTop: 12 }} disabled={createSlabMutation.isPending} onClick={() => createSlabMutation.mutate()}>
            + Add Tax Slab
          </button>
        </div>
      </div>
    </>
  );
}
