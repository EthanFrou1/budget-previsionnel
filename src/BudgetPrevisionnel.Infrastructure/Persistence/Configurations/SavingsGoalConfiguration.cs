using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Configurations;

public class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
{
    public void Configure(EntityTypeBuilder<SavingsGoal> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Label).IsRequired().HasMaxLength(200);
        builder.Property(g => g.TargetAmount).HasPrecision(18, 2);
        builder.Property(g => g.CurrentAmount).HasPrecision(18, 2);

        builder.HasOne(g => g.User)
            .WithMany(u => u.SavingsGoals)
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.LinkedAccount)
            .WithMany()
            .HasForeignKey(g => g.LinkedAccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
