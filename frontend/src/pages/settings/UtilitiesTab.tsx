import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/AuthContext';
import { createPrinter, deletePrinter, listAuditLog, listBackups, listPaymentModes, listPrinters, runBackup, setPaymentModeEnabled } from '../../api/utility';
import { Banner, Switch, useBanner } from '../../components/Shared';
import { formatDateTime } from '../../lib/format';
import { applyTheme, getStoredTheme } from '../../theme';

const THEME_ICON = (
  <svg width="16" height="16" viewBox="0 0 20 20" fill="none">
    <circle cx="10" cy="10" r="4" stroke="currentColor" strokeWidth="1.5" />
    <path d="M10 2.5v2M10 15.5v2M17.5 10h-2M4.5 10h-2M15.1 4.9l-1.4 1.4M6.3 13.7l-1.4 1.4M15.1 15.1l-1.4-1.4M6.3 6.3 4.9 4.9" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
  </svg>
);

const PRINTER_ICON = (
  <svg width="16" height="16" viewBox="0 0 20 20" fill="none">
    <path d="M5.5 7V2.5h9V7M5.5 15.5h-2A1.5 1.5 0 0 1 2 14V9a1.5 1.5 0 0 1 1.5-1.5h13A1.5 1.5 0 0 1 17.5 9v5a1.5 1.5 0 0 1-1.5 1.5h-2" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
    <rect x="5.5" y="12.5" width="9" height="5" stroke="currentColor" strokeWidth="1.5" />
  </svg>
);
const BACKUP_ICON = (
  <svg width="16" height="16" viewBox="0 0 20 20" fill="none">
    <ellipse cx="10" cy="5" rx="6.5" ry="2.4" stroke="currentColor" strokeWidth="1.4" />
    <path d="M3.5 5v10c0 1.3 2.9 2.4 6.5 2.4s6.5-1.1 6.5-2.4V5M3.5 10c0 1.3 2.9 2.4 6.5 2.4s6.5-1.1 6.5-2.4" stroke="currentColor" strokeWidth="1.4" />
  </svg>
);

export function UtilitiesTab() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { banner, fail, succeed, clear } = useBanner();
  const [theme, setTheme] = useState(getStoredTheme);
  const [printerName, setPrinterName] = useState('');
  const [printerType, setPrinterType] = useState<'Receipt' | 'Kot'>('Receipt');
  const [printerStation, setPrinterStation] = useState<'Kitchen' | 'Bar' | ''>('');

  const printersQuery = useQuery({ queryKey: ['printers'], queryFn: () => listPrinters() });
  const paymentModesQuery = useQuery({ queryKey: ['payment-modes'], queryFn: listPaymentModes });
  const backupsQuery = useQuery({ queryKey: ['backups'], queryFn: listBackups });
  const auditQuery = useQuery({ queryKey: ['audit-log'], queryFn: () => listAuditLog() });

  const createPrinterMutation = useMutation({
    mutationFn: () => {
      if (!printerName.trim()) throw new Error('Printer name is required.');
      if (!session?.defaultBranchId) throw new Error('No branch on this session.');
      return createPrinter({
        branchId: session.defaultBranchId,
        name: printerName.trim(),
        printerType,
        station: printerType === 'Kot' && printerStation ? printerStation : null,
        isEnabled: true,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['printers'] });
      succeed('Printer added.');
      setPrinterName('');
    },
    onError: fail,
  });

  const deletePrinterMutation = useMutation({
    mutationFn: (id: string) => deletePrinter(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['printers'] });
    },
    onError: fail,
  });

  const togglePaymentModeMutation = useMutation({
    mutationFn: ({ code, isEnabled }: { code: string; isEnabled: boolean }) => setPaymentModeEnabled(code, isEnabled),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payment-modes'] }),
    onError: fail,
  });

  const runBackupMutation = useMutation({
    mutationFn: runBackup,
    onSuccess: (entry) => {
      queryClient.invalidateQueries({ queryKey: ['backups'] });
      if (entry.status === 'Success') succeed(`Backup completed — ${((entry.sizeBytes ?? 0) / 1024 / 1024).toFixed(1)} MB.`);
      else fail(new Error('Backup failed — check the server-side backup directory configuration.'));
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="stack">
          <div className="card">
            <div className="card-head">
              <div className="card-title">Appearance</div>
            </div>
            <div className="util-row" style={{ borderTop: 'none', paddingTop: 2 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
                <div className="util-ico">{THEME_ICON}</div>
                <div>
                  <div className="util-name">Light Mode</div>
                  <div className="util-sub">Same design, a white background instead of black</div>
                </div>
              </div>
              <Switch
                on={theme === 'light'}
                onToggle={() => {
                  const next = theme === 'light' ? 'dark' : 'light';
                  setTheme(next);
                  applyTheme(next);
                }}
              />
            </div>
          </div>

          <div className="card">
            <div className="card-head">
              <div className="card-title">Printers</div>
            </div>
            {(printersQuery.data ?? []).map((p) => (
              <div className="util-row" key={p.id}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
                  <div className="util-ico">{PRINTER_ICON}</div>
                  <div>
                    <div className="util-name">{p.name}</div>
                    <div className="util-sub">
                      {p.printerType} {p.station ? `· ${p.station}` : ''} · {p.branchName}
                    </div>
                  </div>
                </div>
                <button className="close-x" onClick={() => deletePrinterMutation.mutate(p.id)}>
                  ✕
                </button>
              </div>
            ))}
            {(printersQuery.data ?? []).length === 0 && <div className="page-sub">No printers configured yet.</div>}
            <div className="frow" style={{ marginTop: 14 }}>
              <div className="field" style={{ marginBottom: 0 }}>
                <label>Name</label>
                <input className="input" value={printerName} onChange={(e) => setPrinterName(e.target.value)} placeholder="e.g. Counter Receipt" />
              </div>
              <div className="field" style={{ marginBottom: 0 }}>
                <label>Type</label>
                <select className="input" value={printerType} onChange={(e) => setPrinterType(e.target.value as 'Receipt' | 'Kot')}>
                  <option value="Receipt">Receipt</option>
                  <option value="Kot">KOT</option>
                </select>
              </div>
            </div>
            {printerType === 'Kot' && (
              <div className="field" style={{ marginTop: 12 }}>
                <label>Station</label>
                <select className="input" value={printerStation} onChange={(e) => setPrinterStation(e.target.value as 'Kitchen' | 'Bar' | '')}>
                  <option value="">—</option>
                  <option value="Kitchen">Kitchen</option>
                  <option value="Bar">Bar</option>
                </select>
              </div>
            )}
            <button className="btn btn-ghost btn-block" style={{ marginTop: 12 }} disabled={createPrinterMutation.isPending} onClick={() => createPrinterMutation.mutate()}>
              + Add Printer
            </button>
          </div>

          <div className="card">
            <div className="card-head">
              <div className="card-title">Payment Modes</div>
            </div>
            {(paymentModesQuery.data ?? []).map((pm) => (
              <div className="util-row" key={pm.id}>
                <div className="util-name">{pm.code}</div>
                <Switch on={pm.isEnabled} onToggle={() => togglePaymentModeMutation.mutate({ code: pm.code, isEnabled: !pm.isEnabled })} />
              </div>
            ))}
            {(paymentModesQuery.data ?? []).length === 0 && <div className="page-sub">Loading…</div>}
          </div>
        </div>

        <div className="stack">
          <div className="card">
            <div className="card-head">
              <div className="card-title">Database Backup</div>
              <button className="btn btn-ghost" style={{ padding: '7px 12px', fontSize: 11.5 }} disabled={runBackupMutation.isPending} onClick={() => runBackupMutation.mutate()}>
                {runBackupMutation.isPending ? <span className="spinner" /> : 'Backup Now'}
              </button>
            </div>
            {(backupsQuery.data ?? []).slice(0, 5).map((b) => (
              <div className="util-row" key={b.id}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
                  <div className="util-ico">{BACKUP_ICON}</div>
                  <div>
                    <div className="util-name">{formatDateTime(b.backupAtUtc)}</div>
                    <div className="util-sub">{b.sizeBytes ? `${(b.sizeBytes / 1024 / 1024).toFixed(1)} MB` : 'No size recorded'}</div>
                  </div>
                </div>
                <span className={`badge ${b.status === 'Success' ? 'badge-success' : 'badge-danger'}`}>{b.status}</span>
              </div>
            ))}
            {(backupsQuery.data ?? []).length === 0 && <div className="page-sub">No backups taken yet.</div>}
          </div>

          <div className="card">
            <div className="card-head">
              <div className="card-title">Audit Trail</div>
            </div>
            {(auditQuery.data ?? []).slice(0, 8).map((a) => (
              <div className="util-row" key={a.id} style={{ display: 'block' }}>
                <div className="util-name">
                  {a.action} · {a.entityType}
                </div>
                <div className="util-sub">
                  {a.description} · {formatDateTime(a.occurredAtUtc)}
                </div>
              </div>
            ))}
            {(auditQuery.data ?? []).length === 0 && <div className="page-sub">Nothing logged yet.</div>}
          </div>
        </div>
      </div>
    </>
  );
}
