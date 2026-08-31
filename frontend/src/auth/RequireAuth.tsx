import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

/** `module`, when given, is one of the backend's PermissionModule names
 * (Billing, Masters, Inventory, Transactions, Reports, Workforce,
 * Settings) — a role without canView on it gets bounced to the dashboard
 * rather than the page rendering anyway. AppShell already hides the nav
 * link for the same case; this is what stops someone from just typing the
 * URL instead, since the backend's own [Authorize] only checks "logged
 * in", not per-module permission — this route guard is the only place
 * that's actually enforced today. Omitted for routes every authenticated
 * user can reach regardless of role (Dashboard, Login itself). */
export function RequireAuth({ children, module }: { children: ReactNode; module?: string }) {
  const { isAuthenticated, hasPermission } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (module && !hasPermission(module, 'canView')) return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}
