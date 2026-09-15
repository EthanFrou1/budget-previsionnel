using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Configurations;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.BankName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Label).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Iban).HasMaxLength(34);

        builder.HasOne(a => a.User)
            .WithMany(u => u.BankAccounts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
