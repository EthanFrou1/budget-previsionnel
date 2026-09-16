using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Configurations;

public class RecurringIncomeConfiguration : IEntityTypeConfiguration<RecurringIncome>
{
    public void Configure(EntityTypeBuilder<RecurringIncome> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Label).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        builder.HasOne(e => e.User)
            .WithMany(u => u.RecurringIncomes)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
