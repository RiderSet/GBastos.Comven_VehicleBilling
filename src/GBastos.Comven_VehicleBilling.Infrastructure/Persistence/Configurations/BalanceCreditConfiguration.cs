using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class BalanceCreditConfiguration
    : IEntityTypeConfiguration<BalanceCredit>
{
  public void Configure(EntityTypeBuilder<BalanceCredit> builder)
  {
    builder.ToTable("balance_credits");

    builder.HasKey(item => item.Id);

    builder.Property(item => item.Id)
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(item => item.CustomerId)
        .HasColumnName("customer_id")
        .IsRequired();

    builder.Property(item => item.ExternalReference)
        .HasColumnName("external_reference")
        .HasMaxLength(200)
        .IsRequired();

    builder.HasIndex(item => item.ExternalReference)
        .IsUnique()
        .HasDatabaseName("ux_balance_credits_external_reference");

    builder.Property(item => item.Amount)
        .HasColumnName("amount")
        .HasConversion(
            money => money.Amount,
            amount => Money.Of(amount))
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.Property(item => item.CreatedAt)
        .HasColumnName("created_at")
        .IsRequired();

    builder.HasOne(item => item.Customer)
        .WithMany()
        .HasForeignKey(item => item.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);
  }
}
