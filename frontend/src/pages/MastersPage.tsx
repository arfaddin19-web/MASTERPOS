import { useState } from 'react';
import { AppShell } from '../components/AppShell';
import { Tabs } from '../components/Shared';
import { ProductsTab } from './masters/ProductsTab';
import { LookupTab } from './masters/LookupTab';
import { PartiesTab } from './masters/PartiesTab';
import { TablesTab } from './masters/TablesTab';
import { DiscountOffersTab } from './masters/DiscountOffersTab';

const TABS = ['Products', 'Categories', 'Units', 'Warehouses', 'Parties', 'Dining Tables', 'Discount Offers'] as const;
type Tab = (typeof TABS)[number];

export function MastersPage() {
  const [tab, setTab] = useState<Tab>('Products');

  return (
    <AppShell title="Masters" subtitle="Product, party & configuration records">
      <Tabs tabs={[...TABS]} active={tab} onChange={setTab} />
      {tab === 'Products' && <ProductsTab />}
      {tab === 'Categories' && <LookupTab kind="Category" />}
      {tab === 'Units' && <LookupTab kind="Unit" />}
      {tab === 'Warehouses' && <LookupTab kind="Warehouse" />}
      {tab === 'Parties' && <PartiesTab />}
      {tab === 'Dining Tables' && <TablesTab />}
      {tab === 'Discount Offers' && <DiscountOffersTab />}
    </AppShell>
  );
}
