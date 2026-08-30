import { useState } from 'react';
import { AppShell } from '../components/AppShell';
import { Tabs } from '../components/Shared';
import { PurchaseInvoiceTab } from './transactions/PurchaseInvoiceTab';
import { PurchaseReturnTab } from './transactions/PurchaseReturnTab';
import { JournalEntryTab } from './transactions/JournalEntryTab';
import { PaymentEntryTab } from './transactions/PaymentEntryTab';
import { OpeningBalanceTab } from './transactions/OpeningBalanceTab';
import { ChartOfAccountsTab } from './transactions/ChartOfAccountsTab';

const TABS = ['Purchase Invoice', 'Purchase Return', 'Journal Entry', 'Payment Entry', 'Opening Balance', 'Chart of Accounts'] as const;
type Tab = (typeof TABS)[number];

export function TransactionsPage() {
  const [tab, setTab] = useState<Tab>('Purchase Invoice');

  return (
    <AppShell title="Transactions" subtitle="Purchase, payments & accounting entries">
      <Tabs tabs={[...TABS]} active={tab} onChange={setTab} />
      {tab === 'Purchase Invoice' && <PurchaseInvoiceTab />}
      {tab === 'Purchase Return' && <PurchaseReturnTab />}
      {tab === 'Journal Entry' && <JournalEntryTab />}
      {tab === 'Payment Entry' && <PaymentEntryTab />}
      {tab === 'Opening Balance' && <OpeningBalanceTab />}
      {tab === 'Chart of Accounts' && <ChartOfAccountsTab />}
    </AppShell>
  );
}
