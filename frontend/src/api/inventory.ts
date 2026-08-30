import { apiClient } from './client';
import type { OpeningStockDto, StockAdjustmentDto, StockBalanceDto, StockLedgerEntryDto, StockTransferDto } from './types';

export async function listAdjustments(params?: { productId?: string; warehouseId?: string }) {
  const { data } = await apiClient.get<StockAdjustmentDto[]>('/inventory/adjustments', { params });
  return data;
}

export async function createAdjustment(request: {
  warehouseId: string;
  productId: string;
  quantityChange: number;
  reason: string;
  adjustmentDate: string;
}) {
  const { data } = await apiClient.post<StockAdjustmentDto>('/inventory/adjustments', request);
  return data;
}

export async function listTransfers(status?: string) {
  const { data } = await apiClient.get<StockTransferDto[]>('/inventory/transfers', { params: { status } });
  return data;
}

export async function createTransfer(request: {
  productId: string;
  fromWarehouseId: string;
  toWarehouseId: string;
  quantity: number;
  transferDate: string;
}) {
  const { data } = await apiClient.post<StockTransferDto>('/inventory/transfers', request);
  return data;
}

export async function postTransfer(id: string) {
  const { data } = await apiClient.post<StockTransferDto>(`/inventory/transfers/${id}/post`);
  return data;
}

export async function cancelTransfer(id: string) {
  const { data } = await apiClient.post<StockTransferDto>(`/inventory/transfers/${id}/cancel`);
  return data;
}

export async function listOpeningStock() {
  const { data } = await apiClient.get<OpeningStockDto[]>('/inventory/opening-stock');
  return data;
}

export async function createOpeningStock(request: {
  warehouseId: string;
  productId: string;
  quantity: number;
  unitCost: number;
  asOfDate: string;
}) {
  const { data } = await apiClient.post<OpeningStockDto>('/inventory/opening-stock', request);
  return data;
}

export async function getLedger(params?: { productId?: string; warehouseId?: string; fromDate?: string; toDate?: string }) {
  const { data } = await apiClient.get<StockLedgerEntryDto[]>('/inventory/reports/ledger', { params });
  return data;
}

export async function getBalances(warehouseId?: string) {
  const { data } = await apiClient.get<StockBalanceDto[]>('/inventory/reports/balances', { params: { warehouseId } });
  return data;
}
