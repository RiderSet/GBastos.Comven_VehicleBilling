using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
  public void Configure(EntityTypeBuilder<Customer> builder)
  {
    builder.ToTable("customers");

    builder.HasKey(customer => customer.Id);

    builder.Property(customer => customer.Id)
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(customer => customer.Name)
        .HasColumnName("name")
        .HasMaxLength(200)
        .IsRequired();

    builder.Property(customer => customer.Plan)
        .HasColumnName("plan")
        .HasConversion<int>()
        .IsRequired();

    builder.Property(customer => customer.Balance)
        .HasColumnName("balance")
        .HasConversion(
            money => money.Amount,
            amount => Money.Of(amount))
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.Property(customer => customer.CreatedAt)
        .HasColumnName("created_at")
        .IsRequired();

    builder.HasIndex(customer => customer.Plan)
        .HasDatabaseName("ix_customers_plan");
  }
}
