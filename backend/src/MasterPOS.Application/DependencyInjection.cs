using MasterPOS.Application.Accounting;
using MasterPOS.Application.Auth;
using MasterPOS.Application.Common;
using MasterPOS.Application.Inventory;
using MasterPOS.Application.Masters;
using MasterPOS.Application.Purchase;
using MasterPOS.Application.Reports;
using MasterPOS.Application.Sales;
using MasterPOS.Application.Setup;
using MasterPOS.Application.Utility;
using MasterPOS.Application.Workforce;
using MasterPOS.Domain.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MasterPOS.Application;

/// <summary>
/// Registers everything this layer owns. Api's Program.cs just calls
/// <c>builder.Services.AddApplication()</c> — extend this, not Program.cs,
/// when a new module's service is added.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogger, AuditLogger>();

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductCategoryService, ProductCategoryService>();
        services.AddScoped<IProductGroupService, ProductGroupService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IPartyService, PartyService>();
        services.AddScoped<IDiningTableService, DiningTableService>();

        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IDiscountOfferService, DiscountOfferService>();

        services.AddScoped<IPurchaseInvoiceService, PurchaseInvoiceService>();
        services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();

        services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IOpeningStockService, OpeningStockService>();
        services.AddScoped<IStockReportService, StockReportService>();

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        services.AddScoped<IEmployeeAdvanceService, EmployeeAdvanceService>();
        services.AddScoped<IPayrollSettingsService, PayrollSettingsService>();
        services.AddScoped<ITaxSlabService, TaxSlabService>();
        services.AddScoped<IPayrollRunService, PayrollRunService>();

        services.AddScoped<IChartOfAccountService, ChartOfAccountService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IPartyPaymentService, PartyPaymentService>();
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();

        services.AddScoped<IPrinterService, PrinterService>();
        services.AddScoped<IPaymentModeSettingService, PaymentModeSettingService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddScoped<IBackupService, BackupService>();

        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
