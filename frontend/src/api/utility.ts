import { apiClient } from './client';
import type { AuditLogEntryDto, BackupLogEntryDto, PaymentModeSettingDto, PrinterDto } from './types';

export async function listPrinters(branchId?: string) {
  const { data } = await apiClient.get<PrinterDto[]>('/utility/printers', { params: { branchId } });
  return data;
}

export async function createPrinter(request: {
  branchId: string;
  name: string;
  printerType: 'Receipt' | 'Kot';
  station?: 'Kitchen' | 'Bar' | null;
  connectionInfo?: string | null;
  isEnabled: boolean;
}) {
  const { data } = await apiClient.post<PrinterDto>('/utility/printers', request);
  return data;
}

export async function updatePrinter(
  id: string,
  request: {
    branchId: string;
    name: string;
    printerType: 'Receipt' | 'Kot';
    station?: 'Kitchen' | 'Bar' | null;
    connectionInfo?: string | null;
    isEnabled: boolean;
  },
) {
  const { data } = await apiClient.put<PrinterDto>(`/utility/printers/${id}`, request);
  return data;
}

export async function deletePrinter(id: string) {
  await apiClient.delete(`/utility/printers/${id}`);
}

export async function listPaymentModes() {
  const { data } = await apiClient.get<PaymentModeSettingDto[]>('/utility/payment-modes');
  return data;
}

export async function setPaymentModeEnabled(code: string, isEnabled: boolean) {
  const { data } = await apiClient.patch<PaymentModeSettingDto>(`/utility/payment-modes/${code}`, { isEnabled });
  return data;
}

export async function listAuditLog(params?: { fromDate?: string; toDate?: string; entityType?: string }) {
  const { data } = await apiClient.get<AuditLogEntryDto[]>('/utility/audit-log', { params });
  return data;
}

export async function listBackups() {
  const { data } = await apiClient.get<BackupLogEntryDto[]>('/utility/backups');
  return data;
}

export async function runBackup() {
  const { data } = await apiClient.post<BackupLogEntryDto>('/utility/backups');
  return data;
}
