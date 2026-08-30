import { useCallback, useState, type ReactNode } from 'react';
import { apiErrorMessage } from '../api/client';

/** The gold/red tab strip used throughout Masters/Inventory/Transactions/
 * Reports/Workforce/Settings — one shape (mtabs/wtabs/ttabs/wftabs/stabs in
 * the mockups), one component. */
export function Tabs<T extends string>({ tabs, active, onChange }: { tabs: T[]; active: T; onChange: (t: T) => void }) {
  return (
    <div className="tabstrip">
      {tabs.map((t) => (
        <button key={t} className={`tabstrip-btn${t === active ? ' on' : ''}`} onClick={() => onChange(t)}>
          {t}
        </button>
      ))}
    </div>
  );
}

export function Switch({ on, onToggle, disabled, title }: { on: boolean; onToggle: () => void; disabled?: boolean; title?: string }) {
  return <button type="button" className={`switch${on ? '' : ' off'}`} onClick={onToggle} disabled={disabled} aria-pressed={on} title={title} />;
}

export function Modal({ title, onClose, children, width }: { title: string; onClose: () => void; children: ReactNode; width?: number }) {
  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" style={width ? { width } : undefined} onClick={(e) => e.stopPropagation()}>
        <div className="modal-title">{title}</div>
        {children}
      </div>
    </div>
  );
}

export type BannerState = { kind: 'error' | 'success'; text: string } | null;

/** Every new module page needs the same "show an error/success strip, let
 * mutations report into it" plumbing the POS page established — this is
 * that plumbing factored out so six pages don't reimplement it six times. */
export function useBanner() {
  const [banner, setBanner] = useState<BannerState>(null);
  const fail = useCallback((err: unknown) => setBanner({ kind: 'error', text: apiErrorMessage(err) }), []);
  const succeed = useCallback((text: string) => setBanner({ kind: 'success', text }), []);
  const clear = useCallback(() => setBanner(null), []);
  return { banner, fail, succeed, clear };
}

export function Banner({ banner, onClear }: { banner: BannerState; onClear: () => void }) {
  if (!banner) return null;
  return (
    <div
      className="card"
      style={{
        padding: '10px 16px',
        borderColor: banner.kind === 'error' ? 'var(--danger)' : 'var(--success)',
        color: banner.kind === 'error' ? 'var(--danger)' : 'var(--success)',
        display: 'flex',
        justifyContent: 'space-between',
        flex: 'none',
      }}
    >
      <span>{banner.text}</span>
      <button style={{ border: 'none', background: 'none', color: 'inherit', cursor: 'pointer' }} onClick={onClear}>
        ✕
      </button>
    </div>
  );
}
