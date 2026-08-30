namespace MasterPOS.Domain.Common;

// Every enum here maps 1:1 to a CHECK (... IN (...)) constraint in the
// database schema. Infrastructure/EfConfigurations stores them as the same
// NVARCHAR values via HasConversion<string>() — so the column stays
// readable directly in SSMS, and C# still gets compile-time safety instead
// of magic strings.

public enum BusinessType { Cafe, Trading }

public enum TaxRegistrationType { Vat, Pan }

public enum PermissionModule { Billing, Masters, Inventory, Transactions, Reports, Workforce, Settings }

public enum OrderType { DineIn, Takeaway, Delivery, Counter }

public enum OrderStatus { Open, PartiallyPaid, Paid, Cancelled, OnHold }

public enum KotStation { Kitchen, Bar }

/// <summary>
/// Drives which fields/tabs the product form shows and how a sale affects
/// stock. Inventory = sellable and/or a Recipe's BOM ingredient. Service =
/// non-stock line. Recipe = composite item built from a BOM of Inventory
/// components — selling one deducts the components, not itself. Consumable
/// = stocked for internal operational use only (thermal rolls, stationery)
/// — never sold, never a BOM ingredient.
/// </summary>
public enum ProductType { Inventory, Service, Recipe, Consumable }

public enum KotLineStatus { Pending, Sent, Preparing, Ready, Served }

public enum PaymentMode { Cash, Card, ESewa, Khalti, BankTransfer }

/// <summary>Shared by PurchaseInvoice, PurchaseReturn and JournalEntry.</summary>
public enum DocumentStatus { Draft, Posted, Cancelled }

public enum PartyType { Supplier, Customer, Both }

public enum BalanceType { Dr, Cr }

public enum AccountType { Asset, Liability, Equity, Income, Expense }

public enum DiscountType { Percent, Amount }

public enum StockReferenceType { PurchaseInvoice, PurchaseReturn, Order, Adjustment, TransferOut, TransferIn, OpeningStock }

public enum StockTransferStatus { Pending, Completed, Cancelled }

public enum PartyPaymentDirection { Paid, Received }

public enum PartyPaymentReferenceType { PurchaseInvoice, PurchaseReturn, OpeningBalance }

public enum AttendanceStatus { Present, Late, Absent, OnLeave }

public enum LeaveStatus { Pending, Approved, Rejected }

public enum AdvanceStatus { Open, PartiallyRecovered, Recovered }

public enum PayrollRunStatus { Draft, Completed }

public enum PayrollLineStatus { Ready, LeaveDeduction, AttendancePending }

/// <summary>Monthly is the regular payroll cycle; FestivalBonus is the
/// once-a-year Dashain-style bonus batch — same PayrollRun/PayrollRunLine
/// shape, a different, much simpler calculation (see PayrollRunService).</summary>
public enum PayrollRunType { Monthly, FestivalBonus }

/// <summary>Drives which Nepal income-tax slab table a TDS calculation
/// reads — the government publishes a wider "Couple" band than "Single".</summary>
public enum MaritalStatus { Single, Couple }

public enum PrinterType { Receipt, Kot }

public enum BackupStatus { Success, Failed }
