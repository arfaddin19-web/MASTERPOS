// Mirrors the backend's own DTOs field-for-field (see the record types in
// MasterPOS.Application/**/*Dtos.cs) — kept as plain interfaces, not
// generated, since the backend has no OpenAPI/codegen step yet.

export interface PermissionDto {
  module: string;
  canView: boolean;
  canCreate: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canApprove: boolean;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  fullName: string;
  username: string;
  companyId: string;
  defaultBranchId: string | null;
  roleName: string;
  permissions: PermissionDto[];
}

export interface SetupStatusResponse {
  isSetupComplete: boolean;
}

export interface SetupCompanyRequest {
  companyName: string;
  businessType: 'Cafe' | 'Trading';
  taxRegistrationType: 'Vat' | 'Pan';
  vatRegistrationNumber?: string | null;
  vatRatePercent: number;
  payrollEnabled: boolean;
  branchName: string;
  city?: string | null;
  address?: string | null;
  phone?: string | null;
  adminFullName: string;
  adminUsername: string;
  adminPassword: string;
  adminEmail?: string | null;
}

export interface ProductCategoryDto {
  id: string;
  name: string;
  parentCategoryId: string | null;
}

export interface UnitDto {
  id: string;
  name: string;
  shortCode: string;
}

export interface WarehouseDto {
  id: string;
  name: string;
  branchId: string;
  isDefault: boolean;
}

export type ProductType = 'Inventory' | 'Service' | 'Recipe' | 'Consumable';

export interface ProductDto {
  id: string;
  name: string;
  productType: ProductType;
  categoryId: string | null;
  categoryName: string | null;
  groupId: string | null;
  groupName: string | null;
  unitId: string;
  unitName: string;
  defaultWarehouseId: string | null;
  defaultWarehouseName: string | null;
  barcode: string | null;
  purchasePrice: number;
  salePrice: number;
  isVatApplicable: boolean;
  reorderLevel: number;
  kotStation: 'Kitchen' | 'Bar' | null;
  prepTimeMinutes: number | null;
  trackInPos: boolean;
  isActive: boolean;
}

export interface DiningTableDto {
  id: string;
  branchId: string;
  branchName: string;
  tableNumber: string;
  floorLabel: string | null;
  seats: number;
  status: 'Vacant' | 'Occupied' | 'PartiallyPaid';
}

export type OrderType = 'DineIn' | 'Takeaway' | 'Delivery' | 'Counter';
export type OrderStatus = 'Open' | 'PartiallyPaid' | 'Paid' | 'Cancelled' | 'OnHold';

export interface OrderLineDto {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  note: string | null;
  kotStation: string | null;
  kotStatus: string;
  lineTotalAmount: number;
}

export interface OrderPaymentDto {
  id: string;
  amount: number;
  paymentMode: string;
  paidByLabel: string | null;
  paidAtUtc: string;
}

export interface OrderDto {
  id: string;
  orderNumber: string;
  orderType: OrderType;
  tableId: string | null;
  tableNumber: string | null;
  guestCount: number | null;
  customerId: string | null;
  customerName: string | null;
  status: OrderStatus;
  subTotalAmount: number;
  discountAmount: number;
  vatAmount: number;
  roundOffAmount: number;
  grandTotalAmount: number;
  amountPaid: number;
  amountRemaining: number;
  openedAtUtc: string;
  closedAtUtc: string | null;
  lines: OrderLineDto[];
  payments: OrderPaymentDto[];
}

export interface DiscountOfferDto {
  id: string;
  name: string;
  discountType: 'Percent' | 'Amount';
  value: number;
  validFrom: string | null;
  validTo: string | null;
  isActive: boolean;
}

export interface SalesSummaryDto {
  fromDate: string;
  toDate: string;
  orderCount: number;
  subTotal: number;
  discount: number;
  vat: number;
  grandTotal: number;
  byPaymentMode: { paymentMode: string; amount: number }[];
}

export interface ReorderSuggestionDto {
  productId: string;
  productName: string;
  reorderLevel: number;
  currentBalance: number;
  shortBy: number;
}

// ---- Masters: Groups, Parties, Discount Offers ----

export interface ProductGroupDto {
  id: string;
  name: string;
}

export type PartyType = 'Supplier' | 'Customer' | 'Both';

export interface PartyDto {
  id: string;
  partyType: PartyType;
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  vatOrPanNumber: string | null;
  openingBalanceAmount: number;
  openingBalanceType: 'Dr' | 'Cr';
  loyaltyPoints: number;
  isActive: boolean;
}

// ---- Auth: Roles & Users ----

export type PermissionModule = 'Billing' | 'Masters' | 'Inventory' | 'Transactions' | 'Reports' | 'Workforce' | 'Settings';
export const PERMISSION_MODULES: PermissionModule[] = ['Billing', 'Masters', 'Inventory', 'Transactions', 'Reports', 'Workforce', 'Settings'];

export interface RoleDto {
  id: string;
  name: string;
  isSystemRole: boolean;
  permissions: PermissionDto[];
}

export interface UserDto {
  id: string;
  fullName: string;
  email: string | null;
  username: string;
  roleId: string;
  roleName: string;
  defaultBranchId: string | null;
  defaultBranchName: string | null;
  employeeId: string | null;
  isActive: boolean;
  lastLoginAtUtc: string | null;
}

// ---- Inventory: Adjustments, Transfers, Opening Stock, Ledger ----

export interface StockAdjustmentDto {
  id: string;
  warehouseId: string;
  warehouseName: string;
  productId: string;
  productName: string;
  quantityChange: number;
  reason: string;
  adjustmentDate: string;
}

export type StockTransferStatus = 'Pending' | 'Completed' | 'Cancelled';

export interface StockTransferDto {
  id: string;
  productId: string;
  productName: string;
  fromWarehouseId: string;
  fromWarehouseName: string;
  toWarehouseId: string;
  toWarehouseName: string;
  quantity: number;
  transferDate: string;
  status: StockTransferStatus;
}

export interface OpeningStockDto {
  id: string;
  warehouseId: string;
  warehouseName: string;
  productId: string;
  productName: string;
  quantity: number;
  unitCost: number;
  asOfDate: string;
}

export interface StockLedgerEntryDto {
  id: string;
  movementDate: string;
  productId: string;
  productName: string;
  warehouseId: string;
  warehouseName: string;
  quantityIn: number;
  quantityOut: number;
  runningBalance: number;
  referenceType: string;
  referenceId: string;
}

export interface StockBalanceDto {
  productId: string;
  productName: string;
  warehouseId: string;
  warehouseName: string;
  balance: number;
}

// ---- Purchase ----

export type DocumentStatus = 'Draft' | 'Posted' | 'Cancelled';

export interface PurchaseInvoiceLineDto {
  id: string;
  productId: string;
  productName: string;
  unitId: string;
  unitName: string;
  quantity: number;
  rate: number;
  discountPercent: number;
  vatPercent: number;
  lineAmount: number;
}

export interface PurchaseInvoiceDto {
  id: string;
  invoiceNumber: string;
  supplierId: string;
  supplierName: string;
  supplierReferenceNo: string | null;
  invoiceDate: string;
  paymentTerms: string | null;
  status: DocumentStatus;
  subTotalAmount: number;
  discountAmount: number;
  vatAmount: number;
  roundOffAmount: number;
  grandTotalAmount: number;
  amountPaid: number;
  amountRemaining: number;
  narration: string | null;
  lines: PurchaseInvoiceLineDto[];
}

export interface PurchaseReturnLineDto {
  id: string;
  productId: string;
  productName: string;
  unitId: string;
  unitName: string;
  quantity: number;
  rate: number;
  vatPercent: number;
  lineAmount: number;
}

export interface PurchaseReturnDto {
  id: string;
  returnNumber: string;
  supplierId: string;
  supplierName: string;
  originalPurchaseInvoiceId: string | null;
  returnDate: string;
  status: DocumentStatus;
  subTotalAmount: number;
  vatAmount: number;
  grandTotalAmount: number;
  narration: string | null;
  lines: PurchaseReturnLineDto[];
}

// ---- Accounting ----

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense';

export interface ChartOfAccountDto {
  id: string;
  name: string;
  accountType: AccountType;
  parentAccountId: string | null;
  parentAccountName: string | null;
  isSystemAccount: boolean;
}

export interface JournalEntryLineDto {
  id: string;
  accountId: string;
  accountName: string;
  debitAmount: number;
  creditAmount: number;
  lineNarration: string | null;
}

export interface JournalEntryDto {
  id: string;
  journalNumber: string;
  entryDate: string;
  narration: string | null;
  status: DocumentStatus;
  totalDebit: number;
  totalCredit: number;
  lines: JournalEntryLineDto[];
}

export interface PartyPaymentDto {
  id: string;
  partyId: string;
  partyName: string;
  direction: 'Paid' | 'Received';
  amount: number;
  paymentMode: string;
  referenceType: string | null;
  referenceId: string | null;
  paymentDate: string;
  narration: string | null;
}

export interface OpeningBalanceDto {
  id: string;
  partyId: string | null;
  partyName: string | null;
  accountId: string | null;
  accountName: string | null;
  amount: number;
  balanceType: 'Dr' | 'Cr';
  asOfDate: string;
}

// ---- Reports ----

export interface PurchaseSummaryDto {
  fromDate: string;
  toDate: string;
  invoiceCount: number;
  invoiceTotal: number;
  returnCount: number;
  returnTotal: number;
  netPurchase: number;
}

export interface VatSummaryDto {
  fromDate: string;
  toDate: string;
  salesVatCollected: number;
  purchaseVatPaid: number;
  netVatPayable: number;
}

export interface StockValuationRowDto {
  productId: string;
  productName: string;
  balance: number;
  unitCost: number;
  value: number;
}

export interface StockValuationDto {
  totalValue: number;
  rows: StockValuationRowDto[];
}

export interface TrialBalanceRowDto {
  accountId: string;
  accountName: string;
  accountType: string;
  debit: number;
  credit: number;
}

export interface TrialBalanceDto {
  asOfDate: string;
  totalDebit: number;
  totalCredit: number;
  rows: TrialBalanceRowDto[];
}

// ---- Workforce ----

export type MaritalStatus = 'Single' | 'Couple';

export interface EmployeeDto {
  id: string;
  branchId: string;
  branchName: string;
  fullName: string;
  roleTitle: string | null;
  phone: string | null;
  joinDate: string;
  basicSalary: number;
  shiftStart: string | null;
  shiftEnd: string | null;
  maritalStatus: MaritalStatus;
  isActive: boolean;
}

export interface AttendanceDto {
  id: string;
  employeeId: string;
  employeeName: string;
  attendanceDate: string;
  checkInAtUtc: string | null;
  checkOutAtUtc: string | null;
  status: string;
  overtimeHours: number;
}

export interface TodayAttendanceRowDto {
  employeeId: string;
  employeeName: string;
  shiftStart: string | null;
  shiftEnd: string | null;
  checkInAtUtc: string | null;
  checkOutAtUtc: string | null;
  overtimeHours: number | null;
  status: string | null;
}

export interface LeaveRequestDto {
  id: string;
  employeeId: string;
  employeeName: string;
  leaveType: string;
  fromDate: string;
  toDate: string;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
  approvedByUserId: string | null;
  reason: string | null;
}

export interface EmployeeAdvanceDto {
  id: string;
  employeeId: string;
  employeeName: string;
  amount: number;
  advanceDate: string;
  reason: string | null;
  amountRecovered: number;
  balance: number;
  status: 'Open' | 'PartiallyRecovered' | 'Recovered';
}

export interface PayrollSettingsDto {
  overtimeEnabled: boolean;
  overtimeMultiplier: number;
  pfEnabled: boolean;
  pfEmployeePercent: number;
  pfEmployerPercent: number;
  ssfEnabled: boolean;
  ssfEmployeePercent: number;
  ssfEmployerPercent: number;
  tdsEnabled: boolean;
  festivalBonusEnabled: boolean;
  festivalBonusPercent: number;
}

export interface TaxSlabDto {
  id: string;
  maritalStatus: MaritalStatus;
  lowerBound: number;
  upperBound: number | null;
  ratePercent: number;
}

export interface PayrollRunLineDto {
  id: string;
  employeeId: string;
  employeeName: string;
  roleTitle: string | null;
  basicAmount: number;
  allowancesAmount: number;
  overtimeAmount: number;
  deductionsAmount: number;
  pfEmployeeAmount: number;
  pfEmployerAmount: number;
  ssfEmployeeAmount: number;
  ssfEmployerAmount: number;
  tdsAmount: number;
  advanceDeductionAmount: number;
  netPayAmount: number;
  lineStatus: 'AttendancePending' | 'LeaveDeduction' | 'Ready';
}

export interface PayrollRunDto {
  id: string;
  branchId: string;
  branchName: string;
  periodMonth: number;
  periodYear: number;
  runType: 'Monthly' | 'FestivalBonus';
  status: 'Draft' | 'Completed';
  runAtUtc: string | null;
  grossPayroll: number;
  netPayroll: number;
  lines: PayrollRunLineDto[];
}

// ---- Utility ----

export interface PrinterDto {
  id: string;
  branchId: string;
  branchName: string;
  name: string;
  printerType: 'Receipt' | 'Kot';
  station: 'Kitchen' | 'Bar' | null;
  connectionInfo: string | null;
  isEnabled: boolean;
}

export interface PaymentModeSettingDto {
  id: string;
  code: string;
  isEnabled: boolean;
}

export interface AuditLogEntryDto {
  id: string;
  userId: string;
  action: string;
  entityType: string;
  entityId: string | null;
  description: string;
  occurredAtUtc: string;
}

export interface BackupLogEntryDto {
  id: string;
  backupAtUtc: string;
  filePath: string;
  sizeBytes: number | null;
  triggeredByUserId: string | null;
  status: 'Success' | 'Failed';
}
