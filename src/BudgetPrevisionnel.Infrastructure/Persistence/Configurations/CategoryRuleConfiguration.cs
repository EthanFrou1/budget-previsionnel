using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Configurations;

public class CategoryRuleConfiguration : IEntityTypeConfiguration<CategoryRule>
{
    public void Configure(EntityTypeBuilder<CategoryRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.MatchPattern).IsRequired().HasMaxLength(200);

        builder.HasOne(r => r.Owner)
            .WithMany(u => u.CategoryRules)
            .HasForeignKey(r => r.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
