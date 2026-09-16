using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.RawLabel).IsRequired().HasMaxLength(500);
        builder.Property(t => t.CleanedLabel).HasMaxLength(500);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasOne(t => t.BankAccount)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.BankAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Supports the dashboard's per-account and date-range queries.
        builder.HasIndex(t => new { t.BankAccountId, t.Date });
    }
}
