import { apiClient } from './client';
import type { AccountType, ChartOfAccountDto, JournalEntryDto, OpeningBalanceDto, PartyPaymentDto } from './types';

export async function listAccounts() {
  const { data } = await apiClient.get<ChartOfAccountDto[]>('/accounting/chart-of-accounts');
  return data;
}

export async function createAccount(request: { name: string; accountType: AccountType; parentAccountId?: string | null }) {
  const { data } = await apiClient.post<ChartOfAccountDto>('/accounting/chart-of-accounts', request);
  return data;
}

export async function updateAccount(id: string, request: { name: string; accountType: AccountType; parentAccountId?: string | null }) {
  const { data } = await apiClient.put<ChartOfAccountDto>(`/accounting/chart-of-accounts/${id}`, request);
  return data;
}

export async function deleteAccount(id: string) {
  await apiClient.delete(`/accounting/chart-of-accounts/${id}`);
}

export async function seedDefaultAccounts() {
  const { data } = await apiClient.post<ChartOfAccountDto[]>('/accounting/chart-of-accounts/seed-defaults');
  return data;
}

export async function listJournalEntries(status?: string) {
  const { data } = await apiClient.get<JournalEntryDto[]>('/accounting/journal-entries', { params: { status } });
  return data;
}

export async function getJournalEntry(id: string) {
  const { data } = await apiClient.get<JournalEntryDto>(`/accounting/journal-entries/${id}`);
  return data;
}

export async function createJournalEntry(request: { entryDate: string; narration?: string | null }) {
  const { data } = await apiClient.post<JournalEntryDto>('/accounting/journal-entries', request);
  return data;
}

export async function addJournalLine(
  entryId: string,
  request: { accountId: string; debitAmount: number; creditAmount: number; lineNarration?: string | null },
) {
  const { data } = await apiClient.post<JournalEntryDto>(`/accounting/journal-entries/${entryId}/lines`, request);
  return data;
}

export async function removeJournalLine(entryId: string, lineId: string) {
  const { data } = await apiClient.delete<JournalEntryDto>(`/accounting/journal-entries/${entryId}/lines/${lineId}`);
  return data;
}

export async function postJournalEntry(id: string) {
  const { data } = await apiClient.post<JournalEntryDto>(`/accounting/journal-entries/${id}/post`);
  return data;
}

export async function cancelJournalEntry(id: string) {
  const { data } = await apiClient.post<JournalEntryDto>(`/accounting/journal-entries/${id}/cancel`);
  return data;
}

export async function listPartyPayments(partyId?: string) {
  const { data } = await apiClient.get<PartyPaymentDto[]>('/accounting/party-payments', { params: { partyId } });
  return data;
}

export async function createPartyPayment(request: {
  partyId: string;
  direction: 'Paid' | 'Received';
  amount: number;
  paymentMode: string;
  referenceType?: string | null;
  referenceId?: string | null;
  paymentDate: string;
  narration?: string | null;
}) {
  const { data } = await apiClient.post<PartyPaymentDto>('/accounting/party-payments', request);
  return data;
}

export async function listOpeningBalances() {
  const { data } = await apiClient.get<OpeningBalanceDto[]>('/accounting/opening-balances');
  return data;
}

export async function createOpeningBalance(request: {
  partyId?: string | null;
  accountId?: string | null;
  amount: number;
  balanceType: 'Dr' | 'Cr';
  asOfDate: string;
}) {
  const { data } = await apiClient.post<OpeningBalanceDto>('/accounting/opening-balances', request);
  return data;
}

export async function deleteOpeningBalance(id: string) {
  await apiClient.delete(`/accounting/opening-balances/${id}`);
}
