import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  BillingIcon,
  DashboardIcon,
  InventoryIcon,
  LogoutIcon,
  MastersIcon,
  ReportsIcon,
  SettingsIcon,
  TransactionsIcon,
  WorkforceIcon,
} from './icons';

interface NavItem {
  to: string;
  label: string;
  icon: (props: { className?: string }) => ReactNode;
}

const primaryNav: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: DashboardIcon },
  { to: '/pos', label: 'Billing (POS)', icon: BillingIcon },
  { to: '/masters', label: 'Masters', icon: MastersIcon },
  { to: '/inventory', label: 'Inventory', icon: InventoryIcon },
  { to: '/transactions', label: 'Transactions', icon: TransactionsIcon },
  { to: '/reports', label: 'Reports', icon: ReportsIcon },
];

const peopleNav: NavItem[] = [
  { to: '/workforce', label: 'Workforce', icon: WorkforceIcon },
  { to: '/settings', label: 'Settings', icon: SettingsIcon },
];

function initials(name: string) {
  const parts = name.trim().split(/\s+/);
  return parts.slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('');
}

/** The Main.dc.html shell: sidebar nav + a topbar/content area supplied by
 * whichever route is active. Every authenticated page renders inside this. */
export function AppShell({ title, subtitle, topbarExtra, children }: { title: string; subtitle?: string; topbarExtra?: ReactNode; children: ReactNode }) {
  const { session, logout } = useAuth();

  return (
    <div className="app">
      <div className="sidebar">
        <div className="brand">
          <div className="brand-mark">M</div>
          <div>
            <div className="brand-word">MasterPOS</div>
            <div className="brand-sub">Enterprise Suite</div>
          </div>
        </div>

        <nav className="nav">
          {primaryNav.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}>
              <Icon />
              {label}
            </NavLink>
          ))}
          <div className="nav-section-label">People</div>
          {peopleNav.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}>
              <Icon />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="user-card">
          <div className="avatar">{session ? initials(session.fullName) : ''}</div>
          <div className="user-meta">
            <div className="user-name">{session?.fullName}</div>
            <div className="user-role">{session?.roleName}</div>
          </div>
          <button className="logout-btn" title="Sign out" onClick={logout}>
            <LogoutIcon />
          </button>
        </div>
      </div>

      <div className="main">
        <div className="topbar">
          <div>
            <div className="page-title">{title}</div>
            {subtitle && <div className="page-sub">{subtitle}</div>}
          </div>
          <div className="topbar-right">{topbarExtra}</div>
        </div>
        <div className="content">{children}</div>
      </div>
    </div>
  );
}
