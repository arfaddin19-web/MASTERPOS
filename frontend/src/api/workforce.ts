import { apiClient } from './client';
import type {
  AttendanceDto,
  EmployeeAdvanceDto,
  EmployeeDto,
  LeaveRequestDto,
  MaritalStatus,
  PayrollRunDto,
  PayrollSettingsDto,
  TaxSlabDto,
  TodayAttendanceRowDto,
} from './types';

// ---- Employees ----

export async function listEmployees(activeOnly = false) {
  const { data } = await apiClient.get<EmployeeDto[]>('/workforce/employees', { params: { activeOnly } });
  return data;
}

export interface CreateEmployeeRequest {
  branchId: string;
  fullName: string;
  roleTitle?: string | null;
  phone?: string | null;
  joinDate: string;
  basicSalary: number;
  shiftStart?: string | null;
  shiftEnd?: string | null;
  maritalStatus?: MaritalStatus | null;
}

export interface UpdateEmployeeRequest {
  fullName: string;
  roleTitle?: string | null;
  phone?: string | null;
  basicSalary: number;
  shiftStart?: string | null;
  shiftEnd?: string | null;
  maritalStatus: MaritalStatus;
}

export async function createEmployee(request: CreateEmployeeRequest) {
  const { data } = await apiClient.post<EmployeeDto>('/workforce/employees', request);
  return data;
}

export async function updateEmployee(id: string, request: UpdateEmployeeRequest) {
  const { data } = await apiClient.put<EmployeeDto>(`/workforce/employees/${id}`, request);
  return data;
}

export async function setEmployeeActive(id: string, isActive: boolean) {
  const { data } = await apiClient.patch<EmployeeDto>(`/workforce/employees/${id}/active`, { isActive });
  return data;
}

export async function deleteEmployee(id: string) {
  await apiClient.delete(`/workforce/employees/${id}`);
}

// ---- Attendance ----

export async function listAttendance(params?: { employeeId?: string; fromDate?: string; toDate?: string }) {
  const { data } = await apiClient.get<AttendanceDto[]>('/workforce/attendance', { params });
  return data;
}

export async function getTodayAttendance() {
  const { data } = await apiClient.get<TodayAttendanceRowDto[]>('/workforce/attendance/today');
  return data;
}

export async function checkIn(employeeId: string) {
  const { data } = await apiClient.post<AttendanceDto>('/workforce/attendance/check-in', { employeeId });
  return data;
}

export async function checkOut(attendanceId: string) {
  const { data } = await apiClient.post<AttendanceDto>(`/workforce/attendance/${attendanceId}/check-out`);
  return data;
}

export async function markAttendance(request: {
  employeeId: string;
  attendanceDate: string;
  status: string;
  checkInAtUtc?: string | null;
  checkOutAtUtc?: string | null;
  overtimeHours: number;
}) {
  const { data } = await apiClient.post<AttendanceDto>('/workforce/attendance/mark', request);
  return data;
}

// ---- Leave ----

export async function listLeaveRequests(params?: { employeeId?: string; status?: string }) {
  const { data } = await apiClient.get<LeaveRequestDto[]>('/workforce/leave-requests', { params });
  return data;
}

export async function createLeaveRequest(request: { employeeId: string; leaveType: string; fromDate: string; toDate: string; reason?: string | null }) {
  const { data } = await apiClient.post<LeaveRequestDto>('/workforce/leave-requests', request);
  return data;
}

export async function approveLeave(id: string) {
  const { data } = await apiClient.post<LeaveRequestDto>(`/workforce/leave-requests/${id}/approve`);
  return data;
}

export async function rejectLeave(id: string) {
  const { data } = await apiClient.post<LeaveRequestDto>(`/workforce/leave-requests/${id}/reject`);
  return data;
}

export async function cancelLeave(id: string) {
  const { data } = await apiClient.post<LeaveRequestDto>(`/workforce/leave-requests/${id}/cancel`);
  return data;
}

// ---- Employee Advances ----

export async function listAdvances(params?: { employeeId?: string; status?: string }) {
  const { data } = await apiClient.get<EmployeeAdvanceDto[]>('/workforce/advances', { params });
  return data;
}

export async function createAdvance(request: { employeeId: string; amount: number; advanceDate: string; reason?: string | null }) {
  const { data } = await apiClient.post<EmployeeAdvanceDto>('/workforce/advances', request);
  return data;
}

export async function recoverAdvance(id: string, amount: number) {
  const { data } = await apiClient.post<EmployeeAdvanceDto>(`/workforce/advances/${id}/recover`, { amount });
  return data;
}

// ---- Payroll Settings & Tax Slabs ----

export async function getPayrollSettings() {
  const { data } = await apiClient.get<PayrollSettingsDto>('/workforce/payroll-settings');
  return data;
}

export async function updatePayrollSettings(request: PayrollSettingsDto) {
  const { data } = await apiClient.put<PayrollSettingsDto>('/workforce/payroll-settings', request);
  return data;
}

export async function listTaxSlabs() {
  const { data } = await apiClient.get<TaxSlabDto[]>('/workforce/tax-slabs');
  return data;
}

export async function createTaxSlab(request: { maritalStatus: MaritalStatus; lowerBound: number; upperBound?: number | null; ratePercent: number }) {
  const { data } = await apiClient.post<TaxSlabDto>('/workforce/tax-slabs', request);
  return data;
}

export async function deleteTaxSlab(id: string) {
  await apiClient.delete(`/workforce/tax-slabs/${id}`);
}

export async function seedDefaultTaxSlabs() {
  const { data } = await apiClient.post<TaxSlabDto[]>('/workforce/tax-slabs/seed-defaults');
  return data;
}

// ---- Payroll Runs ----

export async function listPayrollRuns(branchId?: string) {
  const { data } = await apiClient.get<PayrollRunDto[]>('/workforce/payroll-runs', { params: { branchId } });
  return data;
}

export async function getPayrollRun(id: string) {
  const { data } = await apiClient.get<PayrollRunDto>(`/workforce/payroll-runs/${id}`);
  return data;
}

export async function createPayrollRun(request: { branchId: string; periodMonth: number; periodYear: number; runType?: string }) {
  const { data } = await apiClient.post<PayrollRunDto>('/workforce/payroll-runs', request);
  return data;
}

export async function recomputePayrollRun(id: string) {
  const { data } = await apiClient.post<PayrollRunDto>(`/workforce/payroll-runs/${id}/recompute`);
  return data;
}

export async function completePayrollRun(id: string) {
  const { data } = await apiClient.post<PayrollRunDto>(`/workforce/payroll-runs/${id}/complete`);
  return data;
}
