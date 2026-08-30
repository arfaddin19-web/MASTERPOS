using MasterPOS.Domain.Core;
using MasterPOS.Domain.Purchase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> b)
    {
        b.ToTable("PurchaseInvoices", "Purchase", t => t.HasCheckConstraint(
            "CK_PurchaseInvoices_Status", "[Status] IN (N'Draft', N'Posted', N'Cancelled')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.InvoiceNumber).HasMaxLength(30).IsRequired();
        b.Property(e => e.SupplierReferenceNo).HasMaxLength(50);
        b.Property(e => e.PaymentTerms).HasMaxLength(100);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.DocumentStatus.Draft);
        b.Property(e => e.SubTotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.DiscountAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.VatAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.RoundOffAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.GrandTotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.AmountPaid).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.Narration).HasMaxLength(400);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.CompanyId, e.InvoiceNumber }).IsUnique();
        b.HasIndex(e => new { e.CompanyId, e.Status }).HasFilter("[IsDeleted] = 0");
    }
}

public class PurchaseInvoiceLineConfiguration : IEntityTypeConfiguration<PurchaseInvoiceLine>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceLine> b)
    {
        b.ToTable("PurchaseInvoiceLines", "Purchase");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Quantity).HasPrecision(18, 3);
        b.Property(e => e.Rate).HasPrecision(18, 2);
        b.Property(e => e.DiscountPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        b.Property(e => e.VatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        b.Property(e => e.LineAmount).HasPrecision(18, 2);

        b.HasOne(e => e.PurchaseInvoice).WithMany(i => i.Lines).HasForeignKey(e => e.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Unit).WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.PurchaseInvoiceId);
    }
}

public class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> b)
    {
        b.ToTable("PurchaseReturns", "Purchase", t => t.HasCheckConstraint(
            "CK_PurchaseReturns_Status", "[Status] IN (N'Draft', N'Posted', N'Cancelled')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.ReturnNumber).HasMaxLength(30).IsRequired();
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.DocumentStatus.Draft);
        b.Property(e => e.SubTotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.VatAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.GrandTotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.Narration).HasMaxLength(400);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.OriginalPurchaseInvoice).WithMany().HasForeignKey(e => e.OriginalPurchaseInvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.CompanyId, e.ReturnNumber }).IsUnique();
    }
}

public class PurchaseReturnLineConfiguration : IEntityTypeConfiguration<PurchaseReturnLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnLine> b)
    {
        b.ToTable("PurchaseReturnLines", "Purchase");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Quantity).HasPrecision(18, 3);
        b.Property(e => e.Rate).HasPrecision(18, 2);
        b.Property(e => e.VatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        b.Property(e => e.LineAmount).HasPrecision(18, 2);

        b.HasOne(e => e.PurchaseReturn).WithMany(r => r.Lines).HasForeignKey(e => e.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Unit).WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.PurchaseReturnId);
    }
}
