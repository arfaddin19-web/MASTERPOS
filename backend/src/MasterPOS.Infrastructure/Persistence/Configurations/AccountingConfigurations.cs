using MasterPOS.Domain.Core;
using MasterPOS.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b)
    {
        b.ToTable("JournalEntries", "Accounting", t => t.HasCheckConstraint(
            "CK_JournalEntries_Status", "[Status] IN (N'Draft', N'Posted', N'Cancelled')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.JournalNumber).HasMaxLength(30).IsRequired();
        b.Property(e => e.Narration).HasMaxLength(400);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Common.DocumentStatus.Draft);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => new { e.CompanyId, e.JournalNumber }).IsUnique();
    }
}

public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> b)
    {
        b.ToTable("JournalEntryLines", "Accounting", t => t.HasCheckConstraint(
            "CK_JournalEntryLines_OneSided",
            "([DebitAmount] > 0 AND [CreditAmount] = 0) OR ([CreditAmount] > 0 AND [DebitAmount] = 0)"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.DebitAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.CreditAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(e => e.LineNarration).HasMaxLength(300);

        b.HasOne(e => e.JournalEntry).WithMany(j => j.Lines).HasForeignKey(e => e.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.JournalEntryId);
        b.HasIndex(e => e.AccountId);
    }
}

public class PartyPaymentConfiguration : IEntityTypeConfiguration<PartyPayment>
{
    public void Configure(EntityTypeBuilder<PartyPayment> b)
    {
        b.ToTable("PartyPayments", "Accounting", t =>
        {
            t.HasCheckConstraint("CK_PartyPayments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_PartyPayments_Direction", "[Direction] IN (N'Paid', N'Received')");
            t.HasCheckConstraint("CK_PartyPayments_PaymentMode", "[PaymentMode] IN (N'Cash', N'Card', N'eSewa', N'Khalti', N'BankTransfer')");
            t.HasCheckConstraint("CK_PartyPayments_ReferenceType", "[ReferenceType] IS NULL OR [ReferenceType] IN (N'PurchaseInvoice', N'PurchaseReturn', N'OpeningBalance')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Direction).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.PaymentMode).HasConversion(EnumConverters.PaymentMode).HasMaxLength(20).IsRequired();
        b.Property(e => e.ReferenceType).HasConversion<string?>().HasMaxLength(30);
        b.Property(e => e.Narration).HasMaxLength(400);
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Party).WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.PartyId).HasFilter("[IsDeleted] = 0");
    }
}

public class OpeningBalanceConfiguration : IEntityTypeConfiguration<OpeningBalance>
{
    public void Configure(EntityTypeBuilder<OpeningBalance> b)
    {
        b.ToTable("OpeningBalances", "Accounting", t =>
        {
            t.HasCheckConstraint(
                "CK_OpeningBalances_ExactlyOneTarget",
                "(CASE WHEN [PartyId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [AccountId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
            t.HasCheckConstraint("CK_OpeningBalances_BalanceType", "[BalanceType] IN (N'Dr', N'Cr')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.BalanceType).HasConversion<string>().HasMaxLength(2).IsRequired();
        b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAtUtc).HasColumnType("datetime2(3)");
        b.Property(e => e.IsDeleted).HasDefaultValue(false);

        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Party).WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
