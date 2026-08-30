import { useState } from 'react';
import { AppShell } from '../components/AppShell';
import { Tabs } from '../components/Shared';
import { OverviewTab } from './inventory/OverviewTab';
import { AdjustmentsTab } from './inventory/AdjustmentsTab';
import { TransfersTab } from './inventory/TransfersTab';
import { OpeningStockTab } from './inventory/OpeningStockTab';
import { LedgerTab } from './inventory/LedgerTab';

const TABS = ['Overview', 'Adjustments', 'Transfers', 'Opening Stock', 'Ledger'] as const;
type Tab = (typeof TABS)[number];

export function InventoryPage() {
  const [tab, setTab] = useState<Tab>('Overview');

  return (
    <AppShell title="Inventory" subtitle="Stock levels, adjustments & transfers">
      <Tabs tabs={[...TABS]} active={tab} onChange={setTab} />
      {tab === 'Overview' && <OverviewTab />}
      {tab === 'Adjustments' && <AdjustmentsTab />}
      {tab === 'Transfers' && <TransfersTab />}
      {tab === 'Opening Stock' && <OpeningStockTab />}
      {tab === 'Ledger' && <LedgerTab />}
    </AppShell>
  );
}
