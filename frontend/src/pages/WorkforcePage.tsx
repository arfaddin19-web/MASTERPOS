import { useState } from 'react';
import { AppShell } from '../components/AppShell';
import { Tabs } from '../components/Shared';
import { EmployeesTab } from './workforce/EmployeesTab';
import { AttendanceTab } from './workforce/AttendanceTab';
import { LeaveTab } from './workforce/LeaveTab';
import { AdvancesTab } from './workforce/AdvancesTab';
import { PayrollTab } from './workforce/PayrollTab';

const TABS = ['Attendance', 'Leave', 'Advances', 'Payroll', 'Employees'] as const;
type Tab = (typeof TABS)[number];

export function WorkforcePage() {
  const [tab, setTab] = useState<Tab>('Payroll');

  return (
    <AppShell title="Workforce" subtitle="Attendance, leave & payroll">
      <Tabs tabs={[...TABS]} active={tab} onChange={setTab} />
      {tab === 'Attendance' && <AttendanceTab />}
      {tab === 'Leave' && <LeaveTab />}
      {tab === 'Advances' && <AdvancesTab />}
      {tab === 'Payroll' && <PayrollTab />}
      {tab === 'Employees' && <EmployeesTab />}
    </AppShell>
  );
}
