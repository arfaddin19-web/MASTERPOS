using MasterPOS.Domain.Accounting;
using MasterPOS.Domain.Auth;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Inventory;
using MasterPOS.Domain.Masters;
using MasterPOS.Domain.Purchase;
using MasterPOS.Domain.Sales;
using MasterPOS.Domain.Utility;
using MasterPOS.Domain.Workforce;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Infrastructure.Persistence;

public class MasterPosDbContext : DbContext
{
    public MasterPosDbContext(DbContextOptions<MasterPosDbContext> options) : base(options) { }

    // Core / Auth
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();

    // Masters
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<UnitOfMeasure> Units => Set<UnitOfMeasure>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBom> ProductBoms => Set<ProductBom>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<DiscountOffer> DiscountOffers => Set<DiscountOffer>();
    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();

    // Sales / POS
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<KotPrintLog> KotPrintLogs => Set<KotPrintLog>();

    // Purchase
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnLine> PurchaseReturnLines => Set<PurchaseReturnLine>();

    // Accounting
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<PartyPayment> PartyPayments => Set<PartyPayment>();
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();

    // Inventory
    public DbSet<StockLedgerEntry> StockLedgerEntries => Set<StockLedgerEntry>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<OpeningStock> OpeningStocks => Set<OpeningStock>();

    // Workforce / Payroll
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<EmployeeAdvance> EmployeeAdvances => Set<EmployeeAdvance>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollRunLine> PayrollRunLines => Set<PayrollRunLine>();
    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();
    public DbSet<TaxSlab> TaxSlabs => Set<TaxSlab>();

    // Utility
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PaymentModeSetting> PaymentModeSettings => Set<PaymentModeSetting>();
    public DbSet<BackupLogEntry> BackupLogEntries => Set<BackupLogEntry>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterPosDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
