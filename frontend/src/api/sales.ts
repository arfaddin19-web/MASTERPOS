import { apiClient } from './client';
import type { DiscountOfferDto, OrderDto } from './types';

export async function listOpenOrders() {
  const { data } = await apiClient.get<OrderDto[]>('/sales/orders');
  return data;
}

export async function getOrder(id: string) {
  const { data } = await apiClient.get<OrderDto>(`/sales/orders/${id}`);
  return data;
}

export async function createOrder(request: { orderType: string; tableId?: string | null; guestCount?: number | null; customerId?: string | null }) {
  const { data } = await apiClient.post<OrderDto>('/sales/orders', request);
  return data;
}

export async function addLine(orderId: string, request: { productId: string; quantity: number; note?: string | null }) {
  const { data } = await apiClient.post<OrderDto>(`/sales/orders/${orderId}/lines`, request);
  return data;
}

export async function updateLine(orderId: string, lineId: string, request: { quantity: number; note?: string | null }) {
  const { data } = await apiClient.put<OrderDto>(`/sales/orders/${orderId}/lines/${lineId}`, request);
  return data;
}

export async function removeLine(orderId: string, lineId: string) {
  const { data } = await apiClient.delete<OrderDto>(`/sales/orders/${orderId}/lines/${lineId}`);
  return data;
}

export async function applyManualDiscount(orderId: string, discountType: 'Percent' | 'Amount', value: number) {
  const { data } = await apiClient.post<OrderDto>(`/sales/orders/${orderId}/discount/manual`, { discountType, value });
  return data;
}

export async function applyDiscountOffer(orderId: string, discountOfferId: string) {
  const { data } = await apiClient.post<OrderDto>(`/sales/orders/${orderId}/discount/offer`, { discountOfferId });
  return data;
}

export async function clearDiscount(orderId: string) {
  const { data } = await apiClient.delete<OrderDto>(`/sales/orders/${orderId}/discount`);
  return data;
}

export async function printKot(orderId: string) {
  const { data } = await apiClient.post(`/sales/orders/${orderId}/kot`);
  return data;
}

export async function addPayment(orderId: string, request: { amount: number; paymentMode: string; paidByLabel?: string | null }) {
  const { data } = await apiClient.post<OrderDto>(`/sales/orders/${orderId}/payments`, request);
  return data;
}

export async function holdOrder(orderId: string) {
  const { data } = await apiClient.post<OrderDto>(`/sales/orders/${orderId}/hold`);
  return data;
}

export async function cancelOrder(orderId: string) {
  const { data } = await apiClient.post<OrderDto>(`/sales/orders/${orderId}/cancel`);
  return data;
}

export async function listDiscountOffers(activeOnly = true) {
  const { data } = await apiClient.get<DiscountOfferDto[]>('/sales/discount-offers', { params: { activeOnly } });
  return data;
}

export interface UpsertDiscountOfferRequest {
  name: string;
  discountType: 'Percent' | 'Amount';
  value: number;
  validFrom?: string | null;
  validTo?: string | null;
}

export async function createDiscountOffer(request: UpsertDiscountOfferRequest) {
  const { data } = await apiClient.post<DiscountOfferDto>('/sales/discount-offers', request);
  return data;
}

export async function updateDiscountOffer(id: string, request: UpsertDiscountOfferRequest) {
  const { data } = await apiClient.put<DiscountOfferDto>(`/sales/discount-offers/${id}`, request);
  return data;
}

export async function setDiscountOfferActive(id: string, isActive: boolean) {
  const { data } = await apiClient.patch<DiscountOfferDto>(`/sales/discount-offers/${id}/active`, { isActive });
  return data;
}

export async function deleteDiscountOffer(id: string) {
  await apiClient.delete(`/sales/discount-offers/${id}`);
}
