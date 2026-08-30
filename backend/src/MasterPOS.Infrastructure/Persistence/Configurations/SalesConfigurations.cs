using MasterPOS.Domain.Core;
using MasterPOS.Domain.Auth;
using MasterPOS.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders", "Sales", t =>
        {
            t.HasCheckConstraint("CK_Orders_OrderType", "[OrderType] IN (N'DineIn', N'Takeaway', N'Delivery', N'Counter')");
            t.HasCheckConstraint("CK_Orders_Status", "[Status] IN (N'Open', N'PartiallyPaid', N'Paid', N'Cancelled', N'OnHold')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.OrderNumber).HasMaxLength(30).IsRequired();
        b.Property(e => e.OrderType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.OrderStatus.Open);
        b.Property(e => e.SubTotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.DiscountAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.VatAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.RoundOffAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.GrandTotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.OpenedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ClosedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        // AmountPaid / AmountRemaining are computed in C# from Payments — not mapped.
        b.Ignore(e => e.AmountPaid);
        b.Ignore(e => e.AmountRemaining);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Table).WithMany().HasForeignKey(e => e.TableId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.CompanyId, e.OrderNumber }).IsUnique();
        b.HasIndex(e => new { e.CompanyId, e.Status }).HasFilter("[IsDeleted] = 0");
        b.HasIndex(e => e.TableId).HasFilter("[TableId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> b)
    {
        b.ToTable("OrderLines", "Sales", t =>
        {
            t.HasCheckConstraint("CK_OrderLines_KotStation", "[KotStation] IS NULL OR [KotStation] IN (N'Kitchen', N'Bar')");
            t.HasCheckConstraint("CK_OrderLines_KotStatus", "[KotStatus] IN (N'Pending', N'Sent', N'Preparing', N'Ready', N'Served')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Quantity).HasPrecision(18, 3);
        b.Property(e => e.UnitPrice).HasPrecision(18, 2);
        b.Property(e => e.Note).HasMaxLength(300);
        b.Property(e => e.KotStation).HasConversion<string?>().HasMaxLength(20);
        b.Property(e => e.KotStatus).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.KotLineStatus.Pending);
        b.Property(e => e.LineTotalAmount).HasPrecision(18, 2);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Order).WithMany(o => o.Lines).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.OrderId).HasFilter("[IsDeleted] = 0");
    }
}

public class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> b)
    {
        b.ToTable("OrderPayments", "Sales", t =>
        {
            t.HasCheckConstraint("CK_OrderPayments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_OrderPayments_PaymentMode", "[PaymentMode] IN (N'Cash', N'Card', N'eSewa', N'Khalti', N'BankTransfer')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.PaymentMode).HasConversion(EnumConverters.PaymentMode).HasMaxLength(20).IsRequired();
        b.Property(e => e.PaidByLabel).HasMaxLength(50);
        b.Property(e => e.PaidAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Order).WithMany(o => o.Payments).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.OrderId).HasFilter("[IsDeleted] = 0");
    }
}

public class KotPrintLogConfiguration : IEntityTypeConfiguration<KotPrintLog>
{
    public void Configure(EntityTypeBuilder<KotPrintLog> b)
    {
        b.ToTable("KotPrintLog", "Sales", t => t.HasCheckConstraint(
            "CK_KotPrintLog_Station", "[Station] IN (N'Kitchen', N'Bar')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Station).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.PrintedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.IsReprint).HasDefaultValue(false);

        b.HasOne(e => e.Order).WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.PrintedByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.OrderId);
    }
}
