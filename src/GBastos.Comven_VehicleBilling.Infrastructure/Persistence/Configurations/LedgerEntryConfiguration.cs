using Comven_VehicleBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration
    : IEntityTypeConfiguration<LedgerEntry>
{
  public void Configure(EntityTypeBuilder<LedgerEntry> builder)
  {
    builder.ToTable("ledger_entries");

    builder.HasKey(entry => entry.Id);

    builder.Property(entry => entry.Id)
        .ValueGeneratedNever();

    builder.Property(entry => entry.CustomerId)
        .IsRequired();

    builder.Property(entry => entry.OperationId)
        .IsRequired();

    builder.Property(entry => entry.Type)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(entry => entry.Description)
        .HasMaxLength(250)
        .IsRequired();

    builder.Property(entry => entry.CreatedAt)
        .IsRequired();

    builder.OwnsOne(entry => entry.Amount, money =>
    {
      money.Property(value => value.Amount)
          .HasColumnName("amount")
          .HasPrecision(18, 2)
          .IsRequired();
    });

    builder.HasOne(entry => entry.Customer)
        .WithMany()
        .HasForeignKey(entry => entry.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(entry => new
    {
      entry.CustomerId,
      entry.CreatedAt
    })
        .HasDatabaseName("ix_ledger_entries_customer_created_at");

    builder.HasIndex(entry => entry.OperationId)
        .HasDatabaseName("ix_ledger_entries_operation_id");
  }
}
