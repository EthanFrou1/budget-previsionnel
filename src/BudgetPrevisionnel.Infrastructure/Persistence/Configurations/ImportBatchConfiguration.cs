using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName).IsRequired().HasMaxLength(260); // matches Windows MAX_PATH convention

        builder.HasOne(b => b.BankAccount)
            .WithMany(a => a.ImportBatches)
            .HasForeignKey(b => b.BankAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.BankAccountId, b.ImportedAtUtc });
    }
}
