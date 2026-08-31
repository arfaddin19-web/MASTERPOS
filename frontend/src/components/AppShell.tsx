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
  // The backend's PermissionModule this nav destination belongs to (see
  // MasterPOS.Domain.Common.Enums.cs) — omitted for Dashboard, which is
  // just a summary view every authenticated user can see regardless of
  // role. Every other module is real data entry, so it's gated on that
  // role actually having canView for it.
  module?: string;
}

const primaryNav: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: DashboardIcon },
  { to: '/pos', label: 'Billing (POS)', icon: BillingIcon, module: 'Billing' },
  { to: '/masters', label: 'Masters', icon: MastersIcon, module: 'Masters' },
  { to: '/inventory', label: 'Inventory', icon: InventoryIcon, module: 'Inventory' },
  { to: '/transactions', label: 'Transactions', icon: TransactionsIcon, module: 'Transactions' },
  { to: '/reports', label: 'Reports', icon: ReportsIcon, module: 'Reports' },
];

const peopleNav: NavItem[] = [
  { to: '/workforce', label: 'Workforce', icon: WorkforceIcon, module: 'Workforce' },
  { to: '/settings', label: 'Settings', icon: SettingsIcon, module: 'Settings' },
];

function initials(name: string) {
  const parts = name.trim().split(/\s+/);
  return parts.slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('');
}

/** The Main.dc.html shell: sidebar nav + a topbar/content area supplied by
 * whichever route is active. Every authenticated page renders inside this. */
export function AppShell({ title, subtitle, topbarExtra, children }: { title: string; subtitle?: string; topbarExtra?: ReactNode; children: ReactNode }) {
  const { session, logout, hasPermission } = useAuth();

  // A role with canView off for a module shouldn't see it offered in the
  // sidebar at all — previously every nav item rendered unconditionally
  // regardless of the logged-in role's actual permissions, which is what
  // made every restricted role look identical to Admin in the UI.
  const visiblePrimaryNav = primaryNav.filter((item) => !item.module || hasPermission(item.module, 'canView'));
  const visiblePeopleNav = peopleNav.filter((item) => !item.module || hasPermission(item.module, 'canView'));

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
          {visiblePrimaryNav.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}>
              <Icon />
              {label}
            </NavLink>
          ))}
          {visiblePeopleNav.length > 0 && <div className="nav-section-label">People</div>}
          {visiblePeopleNav.map(({ to, label, icon: Icon }) => (
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
