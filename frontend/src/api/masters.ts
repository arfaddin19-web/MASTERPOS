import { apiClient } from './client';
import type {
  DiningTableDto,
  PartyDto,
  PartyType,
  ProductCategoryDto,
  ProductDto,
  ProductGroupDto,
  ProductType,
  UnitDto,
  WarehouseDto,
} from './types';

export async function listProducts(params?: { search?: string; categoryId?: string; productType?: string }) {
  const { data } = await apiClient.get<ProductDto[]>('/masters/products', { params });
  return data;
}

export interface UpsertProductRequest {
  name: string;
  productType: ProductType;
  categoryId?: string | null;
  groupId?: string | null;
  unitId: string;
  defaultWarehouseId?: string | null;
  barcode?: string | null;
  purchasePrice: number;
  salePrice: number;
  isVatApplicable: boolean;
  reorderLevel: number;
  kotStation?: 'Kitchen' | 'Bar' | null;
  prepTimeMinutes?: number | null;
  trackInPos: boolean;
  isActive: boolean;
}

export async function createProduct(request: UpsertProductRequest) {
  const { data } = await apiClient.post<ProductDto>('/masters/products', request);
  return data;
}

export async function updateProduct(id: string, request: UpsertProductRequest) {
  const { data } = await apiClient.put<ProductDto>(`/masters/products/${id}`, request);
  return data;
}

export async function setProductActive(id: string, isActive: boolean) {
  const { data } = await apiClient.patch<ProductDto>(`/masters/products/${id}/active`, { isActive });
  return data;
}

export async function deleteProduct(id: string) {
  await apiClient.delete(`/masters/products/${id}`);
}

export interface ProductBomLineDto {
  componentProductId: string;
  componentProductName: string;
  unitName: string;
  quantity: number;
}

export async function getProductBom(id: string) {
  const { data } = await apiClient.get<ProductBomLineDto[]>(`/masters/products/${id}/bom`);
  return data;
}

export async function setProductBom(id: string, lines: { componentProductId: string; quantity: number }[]) {
  const { data } = await apiClient.put<ProductBomLineDto[]>(`/masters/products/${id}/bom`, { lines });
  return data;
}

export async function listCategories() {
  const { data } = await apiClient.get<ProductCategoryDto[]>('/masters/categories');
  return data;
}

export async function createCategory(name: string, parentCategoryId?: string | null) {
  const { data } = await apiClient.post<ProductCategoryDto>('/masters/categories', { name, parentCategoryId });
  return data;
}

export async function listGroups() {
  const { data } = await apiClient.get<ProductGroupDto[]>('/masters/groups');
  return data;
}

export async function createGroup(name: string) {
  const { data } = await apiClient.post<ProductGroupDto>('/masters/groups', { name });
  return data;
}

export async function listUnits() {
  const { data } = await apiClient.get<UnitDto[]>('/masters/units');
  return data;
}

export async function createUnit(name: string, shortCode?: string | null) {
  const { data } = await apiClient.post<UnitDto>('/masters/units', { name, shortCode });
  return data;
}

export async function listWarehouses() {
  const { data } = await apiClient.get<WarehouseDto[]>('/masters/warehouses');
  return data;
}

export async function createWarehouse(name: string, branchId: string, isDefault: boolean) {
  const { data } = await apiClient.post<WarehouseDto>('/masters/warehouses', { name, branchId, isDefault });
  return data;
}

export async function listTables(branchId?: string) {
  const { data } = await apiClient.get<DiningTableDto[]>('/masters/tables', { params: { branchId } });
  return data;
}

export async function createTable(request: { branchId: string; tableNumber: string; floorLabel?: string | null; seats: number }) {
  const { data } = await apiClient.post<DiningTableDto>('/masters/tables', request);
  return data;
}

export async function updateTable(id: string, request: { tableNumber: string; floorLabel?: string | null; seats: number }) {
  const { data } = await apiClient.put<DiningTableDto>(`/masters/tables/${id}`, request);
  return data;
}

export async function deleteTable(id: string) {
  await apiClient.delete(`/masters/tables/${id}`);
}

export interface UpsertPartyRequest {
  partyType: PartyType;
  name: string;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  vatOrPanNumber?: string | null;
  openingBalanceAmount: number;
  openingBalanceType: 'Dr' | 'Cr';
}

export async function listParties(params?: { partyType?: string; activeOnly?: boolean }) {
  const { data } = await apiClient.get<PartyDto[]>('/masters/parties', { params });
  return data;
}

export async function createParty(request: UpsertPartyRequest) {
  const { data } = await apiClient.post<PartyDto>('/masters/parties', request);
  return data;
}

export async function updateParty(id: string, request: UpsertPartyRequest) {
  const { data } = await apiClient.put<PartyDto>(`/masters/parties/${id}`, request);
  return data;
}

export async function setPartyActive(id: string, isActive: boolean) {
  const { data } = await apiClient.patch<PartyDto>(`/masters/parties/${id}/active`, { isActive });
  return data;
}

export async function deleteParty(id: string) {
  await apiClient.delete(`/masters/parties/${id}`);
}
