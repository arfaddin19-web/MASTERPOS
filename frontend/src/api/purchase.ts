import { apiClient } from './client';
import type { PurchaseInvoiceDto, PurchaseReturnDto } from './types';

export async function listInvoices(status?: string) {
  const { data } = await apiClient.get<PurchaseInvoiceDto[]>('/purchase/invoices', { params: { status } });
  return data;
}

export async function getInvoice(id: string) {
  const { data } = await apiClient.get<PurchaseInvoiceDto>(`/purchase/invoices/${id}`);
  return data;
}

export async function createInvoice(request: {
  supplierId: string;
  supplierReferenceNo?: string | null;
  invoiceDate: string;
  paymentTerms?: string | null;
  narration?: string | null;
}) {
  const { data } = await apiClient.post<PurchaseInvoiceDto>('/purchase/invoices', request);
  return data;
}

export async function addInvoiceLine(
  invoiceId: string,
  request: { productId: string; unitId: string; quantity: number; rate: number; discountPercent: number; vatPercent: number },
) {
  const { data } = await apiClient.post<PurchaseInvoiceDto>(`/purchase/invoices/${invoiceId}/lines`, request);
  return data;
}

export async function updateInvoiceLine(
  invoiceId: string,
  lineId: string,
  request: { unitId: string; quantity: number; rate: number; discountPercent: number; vatPercent: number },
) {
  const { data } = await apiClient.put<PurchaseInvoiceDto>(`/purchase/invoices/${invoiceId}/lines/${lineId}`, request);
  return data;
}

export async function removeInvoiceLine(invoiceId: string, lineId: string) {
  const { data } = await apiClient.delete<PurchaseInvoiceDto>(`/purchase/invoices/${invoiceId}/lines/${lineId}`);
  return data;
}

export async function postInvoice(id: string) {
  const { data } = await apiClient.post<PurchaseInvoiceDto>(`/purchase/invoices/${id}/post`);
  return data;
}

export async function cancelInvoice(id: string) {
  const { data } = await apiClient.post<PurchaseInvoiceDto>(`/purchase/invoices/${id}/cancel`);
  return data;
}

export async function recordInvoicePayment(id: string, amount: number) {
  const { data } = await apiClient.post<PurchaseInvoiceDto>(`/purchase/invoices/${id}/payments`, { amount });
  return data;
}

export async function listReturns(status?: string) {
  const { data } = await apiClient.get<PurchaseReturnDto[]>('/purchase/returns', { params: { status } });
  return data;
}

export async function getReturn(id: string) {
  const { data } = await apiClient.get<PurchaseReturnDto>(`/purchase/returns/${id}`);
  return data;
}

export async function createReturn(request: {
  supplierId: string;
  originalPurchaseInvoiceId?: string | null;
  returnDate: string;
  narration?: string | null;
}) {
  const { data } = await apiClient.post<PurchaseReturnDto>('/purchase/returns', request);
  return data;
}

export async function addReturnLine(
  returnId: string,
  request: { productId: string; unitId: string; quantity: number; rate: number; vatPercent: number },
) {
  const { data } = await apiClient.post<PurchaseReturnDto>(`/purchase/returns/${returnId}/lines`, request);
  return data;
}

export async function removeReturnLine(returnId: string, lineId: string) {
  const { data } = await apiClient.delete<PurchaseReturnDto>(`/purchase/returns/${returnId}/lines/${lineId}`);
  return data;
}

export async function postReturn(id: string) {
  const { data } = await apiClient.post<PurchaseReturnDto>(`/purchase/returns/${id}/post`);
  return data;
}

export async function cancelReturn(id: string) {
  const { data } = await apiClient.post<PurchaseReturnDto>(`/purchase/returns/${id}/cancel`);
  return data;
}
