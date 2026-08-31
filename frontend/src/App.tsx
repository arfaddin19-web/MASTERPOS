import { useQuery } from '@tanstack/react-query';
import { Navigate, Route, Routes } from 'react-router-dom';
import { getSetupStatus } from './api/setup';
import { RequireAuth } from './auth/RequireAuth';
import { LoginPage } from './pages/LoginPage';
import { SetupPage } from './pages/SetupPage';
import { DashboardPage } from './pages/DashboardPage';
import { PosPage } from './pages/PosPage';
import { MastersPage } from './pages/MastersPage';
import { InventoryPage } from './pages/InventoryPage';
import { TransactionsPage } from './pages/TransactionsPage';
import { ReportsPage } from './pages/ReportsPage';
import { WorkforcePage } from './pages/WorkforcePage';
import { SettingsPage } from './pages/SettingsPage';

/** The very first thing the app checks — no company yet means every route
 * except /setup is meaningless, so gate the whole tree on it once at boot. */
function RootRedirect() {
  const { data, isLoading } = useQuery({ queryKey: ['setup-status'], queryFn: getSetupStatus });
  if (isLoading) {
    return (
      <div className="empty-state" style={{ height: '100vh' }}>
        <span className="spinner" />
      </div>
    );
  }
  return <Navigate to={data?.isSetupComplete ? '/login' : '/setup'} replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/setup" element={<SetupPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/dashboard" element={<RequireAuth><DashboardPage /></RequireAuth>} />
      <Route path="/pos" element={<RequireAuth module="Billing"><PosPage /></RequireAuth>} />
      <Route path="/masters" element={<RequireAuth module="Masters"><MastersPage /></RequireAuth>} />
      <Route path="/inventory" element={<RequireAuth module="Inventory"><InventoryPage /></RequireAuth>} />
      <Route path="/transactions" element={<RequireAuth module="Transactions"><TransactionsPage /></RequireAuth>} />
      <Route path="/reports" element={<RequireAuth module="Reports"><ReportsPage /></RequireAuth>} />
      <Route path="/workforce" element={<RequireAuth module="Workforce"><WorkforcePage /></RequireAuth>} />
      <Route path="/settings" element={<RequireAuth module="Settings"><SettingsPage /></RequireAuth>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
