using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;
using MasterPOS.Domain.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> b)
    {
        b.ToTable("ProductCategories", "Masters");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.ParentCategory).WithMany().HasForeignKey(e => e.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CompanyId, e.Name }).IsUnique();
    }
}

public class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    public void Configure(EntityTypeBuilder<ProductGroup> b)
    {
        b.ToTable("ProductGroups", "Masters");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CompanyId, e.Name }).IsUnique();
    }
}

public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> b)
    {
        b.ToTable("Units", "Masters"); // C# name UnitOfMeasure, DB table Units — see class remarks
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(50).IsRequired();
        b.Property(e => e.ShortCode).HasMaxLength(10);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CompanyId, e.Name }).IsUnique();
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("Warehouses", "Masters");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
        b.Property(e => e.IsDefault).HasDefaultValue(false);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products", "Masters", t =>
        {
            t.HasCheckConstraint("CK_Products_KotStation", "[KotStation] IS NULL OR [KotStation] IN (N'Kitchen', N'Bar')");
            t.HasCheckConstraint("CK_Products_ProductType", "[ProductType] IN (N'Inventory', N'Service', N'Recipe', N'Consumable')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.ProductType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ProductType.Inventory);
        b.Property(e => e.Barcode).HasMaxLength(50);
        b.Property(e => e.PurchasePrice).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.SalePrice).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.IsVatApplicable).HasDefaultValue(true);
        b.Property(e => e.ReorderLevel).HasPrecision(18, 3).HasDefaultValue(0m);
        b.Property(e => e.KotStation).HasConversion<string?>().HasMaxLength(20);
        b.Property(e => e.ImagePath).HasMaxLength(400);
        b.Property(e => e.TrackInPos).HasDefaultValue(true);
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Category).WithMany(c => c.Products).HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Group).WithMany(g => g.Products).HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Unit).WithMany(u => u.Products).HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.DefaultWarehouse).WithMany().HasForeignKey(e => e.DefaultWarehouseId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.CompanyId).HasFilter("[IsDeleted] = 0");
        b.HasIndex(e => new { e.CompanyId, e.Barcode }).IsUnique().HasFilter("[Barcode] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class ProductBomConfiguration : IEntityTypeConfiguration<ProductBom>
{
    public void Configure(EntityTypeBuilder<ProductBom> b)
    {
        b.ToTable("ProductBom", "Masters", t => t.HasCheckConstraint(
            "CK_ProductBom_NotSelf", "[FinishedProductId] <> [ComponentProductId]"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Quantity).HasPrecision(18, 3);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.FinishedProduct).WithMany(p => p.BomComponents)
            .HasForeignKey(e => e.FinishedProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.ComponentProduct).WithMany(p => p.UsedInRecipes)
            .HasForeignKey(e => e.ComponentProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> b)
    {
        b.ToTable("DiningTables", "Masters", t => t.HasCheckConstraint(
            "CK_DiningTables_Status", "[Status] IN (N'Vacant', N'Occupied', N'PartiallyPaid')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.TableNumber).HasMaxLength(20).IsRequired();
        b.Property(e => e.FloorLabel).HasMaxLength(50);
        b.Property(e => e.Seats).HasDefaultValue(4);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(DiningTableStatus.Vacant);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.BranchId, e.TableNumber }).IsUnique();
        b.HasIndex(e => new { e.BranchId, e.Status }).HasFilter("[IsDeleted] = 0");
    }
}

public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> b)
    {
        b.ToTable("Parties", "Masters", t =>
        {
            t.HasCheckConstraint("CK_Parties_PartyType", "[PartyType] IN (N'Supplier', N'Customer', N'Both')");
            t.HasCheckConstraint("CK_Parties_OpeningBalanceType", "[OpeningBalanceType] IN (N'Dr', N'Cr')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.PartyType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Phone).HasMaxLength(30);
        b.Property(e => e.Email).HasMaxLength(200);
        b.Property(e => e.Address).HasMaxLength(300);
        b.Property(e => e.VatOrPanNumber).HasMaxLength(30);
        b.Property(e => e.OpeningBalanceAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.OpeningBalanceType).HasConversion<string>().HasMaxLength(2).HasDefaultValue(BalanceType.Dr);
        b.Property(e => e.LoyaltyPoints).HasDefaultValue(0);
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CompanyId, e.PartyType }).HasFilter("[IsDeleted] = 0");
    }
}

public class DiscountOfferConfiguration : IEntityTypeConfiguration<DiscountOffer>
{
    public void Configure(EntityTypeBuilder<DiscountOffer> b)
    {
        b.ToTable("DiscountOffers", "Masters", t => t.HasCheckConstraint(
            "CK_DiscountOffers_DiscountType", "[DiscountType] IN (N'Percent', N'Amount')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
        b.Property(e => e.DiscountType).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(e => e.Value).HasPrecision(18, 2);
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
{
    public void Configure(EntityTypeBuilder<ChartOfAccount> b)
    {
        b.ToTable("ChartOfAccounts", "Masters", t => t.HasCheckConstraint(
            "CK_ChartOfAccounts_AccountType", "[AccountType] IN (N'Asset', N'Liability', N'Equity', N'Income', N'Expense')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
        b.Property(e => e.AccountType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.IsSystemAccount).HasDefaultValue(false);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.ParentAccount).WithMany().HasForeignKey(e => e.ParentAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
