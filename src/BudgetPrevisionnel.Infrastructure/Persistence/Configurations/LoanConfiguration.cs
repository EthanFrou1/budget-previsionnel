using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Label).IsRequired().HasMaxLength(200);
        builder.Property(l => l.PrincipalAmount).HasPrecision(18, 2);
        builder.Property(l => l.RemainingAmount).HasPrecision(18, 2);
        builder.Property(l => l.InterestRate).HasPrecision(7, 4);
        builder.Property(l => l.MonthlyPayment).HasPrecision(18, 2);

        builder.HasOne(l => l.User)
            .WithMany(u => u.Loans)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
