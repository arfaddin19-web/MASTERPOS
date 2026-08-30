import { apiClient } from './client';
import type { PurchaseSummaryDto, ReorderSuggestionDto, SalesSummaryDto, StockValuationDto, TrialBalanceDto, VatSummaryDto } from './types';

export async function getSalesSummary(fromDate: string, toDate: string) {
  const { data } = await apiClient.get<SalesSummaryDto>('/reports/sales-summary', { params: { fromDate, toDate } });
  return data;
}

export async function getPurchaseSummary(fromDate: string, toDate: string) {
  const { data } = await apiClient.get<PurchaseSummaryDto>('/reports/purchase-summary', { params: { fromDate, toDate } });
  return data;
}

export async function getVatSummary(fromDate: string, toDate: string) {
  const { data } = await apiClient.get<VatSummaryDto>('/reports/vat-summary', { params: { fromDate, toDate } });
  return data;
}

export async function getStockValuation(warehouseId?: string) {
  const { data } = await apiClient.get<StockValuationDto>('/reports/stock-valuation', { params: { warehouseId } });
  return data;
}

export async function getTrialBalance(asOfDate: string) {
  const { data } = await apiClient.get<TrialBalanceDto>('/reports/trial-balance', { params: { asOfDate } });
  return data;
}

export async function getReorderSuggestions() {
  const { data } = await apiClient.get<ReorderSuggestionDto[]>('/inventory/reports/reorder-suggestions');
  return data;
}
