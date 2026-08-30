using MasterPOS.Domain.Core;
using MasterPOS.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class StockLedgerEntryConfiguration : IEntityTypeConfiguration<StockLedgerEntry>
{
    public void Configure(EntityTypeBuilder<StockLedgerEntry> b)
    {
        b.ToTable("StockLedgerEntries", "Inventory", t => t.HasCheckConstraint(
            "CK_StockLedgerEntries_ReferenceType",
            "[ReferenceType] IN (N'PurchaseInvoice', N'PurchaseReturn', N'Order', N'Adjustment', N'TransferOut', N'TransferIn', N'OpeningStock')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.QuantityIn).HasPrecision(18, 3).HasDefaultValue(0m);
        b.Property(e => e.QuantityOut).HasPrecision(18, 3).HasDefaultValue(0m);
        b.Property(e => e.ReferenceType).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.ProductId, e.WarehouseId, e.MovementDate });
    }
}

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> b)
    {
        b.ToTable("StockAdjustments", "Inventory");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.QuantityChange).HasPrecision(18, 3);
        b.Property(e => e.Reason).HasMaxLength(200).IsRequired();
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> b)
    {
        b.ToTable("StockTransfers", "Inventory", t =>
        {
            t.HasCheckConstraint("CK_StockTransfers_DifferentWarehouses", "[FromWarehouseId] <> [ToWarehouseId]");
            t.HasCheckConstraint("CK_StockTransfers_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_StockTransfers_Status", "[Status] IN (N'Pending', N'Completed', N'Cancelled')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Quantity).HasPrecision(18, 3);
        // No .HasDefaultValue() here deliberately: Completed (not Pending) is
        // the intended default, but Pending is also the enum's CLR default(0)
        // — pairing HasDefaultValue with that would make EF treat an explicit
        // "Status = Pending" as "unset" and silently coerce it to Completed on
        // insert. StockTransfer.Status already defaults to Completed in C#,
        // which is enough since every insert goes through the application.
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.FromWarehouse).WithMany().HasForeignKey(e => e.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.ToWarehouse).WithMany().HasForeignKey(e => e.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OpeningStockConfiguration : IEntityTypeConfiguration<OpeningStock>
{
    public void Configure(EntityTypeBuilder<OpeningStock> b)
    {
        b.ToTable("OpeningStock", "Inventory");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Quantity).HasPrecision(18, 3);
        b.Property(e => e.UnitCost).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.WarehouseId, e.ProductId }).IsUnique();
    }
}
