import { useState } from 'react';
import { AppShell } from '../components/AppShell';
import { Tabs } from '../components/Shared';
import { RolesTab } from './settings/RolesTab';
import { UsersTab } from './settings/UsersTab';
import { UtilitiesTab } from './settings/UtilitiesTab';

const TABS = ['Roles & Permissions', 'Users', 'Utilities'] as const;
type Tab = (typeof TABS)[number];

export function SettingsPage() {
  const [tab, setTab] = useState<Tab>('Roles & Permissions');

  return (
    <AppShell title="Settings" subtitle="Users, roles & system utilities">
      <Tabs tabs={[...TABS]} active={tab} onChange={setTab} />
      {tab === 'Roles & Permissions' && <RolesTab />}
      {tab === 'Users' && <UsersTab />}
      {tab === 'Utilities' && <UtilitiesTab />}
    </AppShell>
  );
}
