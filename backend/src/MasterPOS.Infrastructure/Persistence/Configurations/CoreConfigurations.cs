using MasterPOS.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Companies", "Core", t =>
        {
            t.HasCheckConstraint("CK_Companies_BusinessType", "[BusinessType] IN (N'Cafe', N'Trading')");
            t.HasCheckConstraint("CK_Companies_TaxRegistrationType", "[TaxRegistrationType] IN (N'VAT', N'PAN')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.BusinessType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.TaxRegistrationType).HasConversion(EnumConverters.TaxRegistrationType).HasMaxLength(10).IsRequired();
        b.Property(e => e.VatRegistrationNumber).HasMaxLength(30);
        b.Property(e => e.VatRatePercent).HasPrecision(5, 2).HasDefaultValue(13.00m);
        b.Property(e => e.PrimaryCurrencyCode).HasMaxLength(3).HasDefaultValue("NPR");
        b.Property(e => e.PayrollEnabled).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branches", "Core");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
        b.Property(e => e.City).HasMaxLength(100);
        b.Property(e => e.Address).HasMaxLength(300);
        b.Property(e => e.Phone).HasMaxLength(30);
        b.Property(e => e.IsPrimary).HasDefaultValue(false);
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne(e => e.Company).WithMany(c => c.Branches)
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.CompanyId).HasFilter("[IsDeleted] = 0");
    }
}
