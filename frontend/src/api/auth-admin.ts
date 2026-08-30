// Roles & Users — the Settings screen's "Roles & Permissions" and "Users" tabs.
// Named auth-admin (not auth.ts) since auth.ts is the login call every page needs;
// these are the admin-only management endpoints under the same /auth/* prefix.
import { apiClient } from './client';
import type { PermissionDto, RoleDto, UserDto } from './types';

export async function listRoles() {
  const { data } = await apiClient.get<RoleDto[]>('/auth/roles');
  return data;
}

export interface UpsertRoleRequest {
  name: string;
  permissions: PermissionDto[];
}

export async function createRole(request: UpsertRoleRequest) {
  const { data } = await apiClient.post<RoleDto>('/auth/roles', request);
  return data;
}

export async function updateRole(id: string, request: UpsertRoleRequest) {
  const { data } = await apiClient.put<RoleDto>(`/auth/roles/${id}`, request);
  return data;
}

export async function deleteRole(id: string) {
  await apiClient.delete(`/auth/roles/${id}`);
}

export async function listUsers(activeOnly = false) {
  const { data } = await apiClient.get<UserDto[]>('/auth/users', { params: { activeOnly } });
  return data;
}

export interface CreateUserRequest {
  fullName: string;
  email?: string | null;
  username: string;
  password: string;
  roleId: string;
  defaultBranchId?: string | null;
  employeeId?: string | null;
}

export async function createUser(request: CreateUserRequest) {
  const { data } = await apiClient.post<UserDto>('/auth/users', request);
  return data;
}

export interface UpdateUserRequest {
  fullName: string;
  email?: string | null;
  roleId: string;
  defaultBranchId?: string | null;
  employeeId?: string | null;
}

export async function updateUser(id: string, request: UpdateUserRequest) {
  const { data } = await apiClient.put<UserDto>(`/auth/users/${id}`, request);
  return data;
}

export async function setUserActive(id: string, isActive: boolean) {
  const { data } = await apiClient.patch<UserDto>(`/auth/users/${id}/active`, { isActive });
  return data;
}

export async function resetPassword(id: string, newPassword: string) {
  await apiClient.post(`/auth/users/${id}/reset-password`, { newPassword });
}
