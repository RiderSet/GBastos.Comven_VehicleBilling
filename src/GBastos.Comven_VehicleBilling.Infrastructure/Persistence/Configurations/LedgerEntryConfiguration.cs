using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
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
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(entry => entry.OperationId)
        .HasColumnName("operation_id")
        .IsRequired();

    builder.Property(entry => entry.CustomerId)
        .HasColumnName("customer_id")
        .IsRequired();

    builder.Property(entry => entry.Type)
        .HasColumnName("type")
        .HasConversion<int>()
        .IsRequired();

    builder.Property(entry => entry.Amount)
        .HasColumnName("amount")
        .HasConversion(
            money => money.Amount,
            amount => Money.Of(amount))
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.Property(entry => entry.CreatedAt)
        .HasColumnName("created_at")
        .IsRequired();

    builder.HasOne(entry => entry.Operation)
        .WithMany()
        .HasForeignKey(entry => entry.OperationId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(entry => entry.Customer)
        .WithMany()
        .HasForeignKey(entry => entry.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    // No máximo um débito e um crédito por operação.
    builder.HasIndex(entry => new
    {
      entry.OperationId,
      entry.Type
    })
        .IsUnique()
        .HasDatabaseName("ux_ledger_entries_operation_type");

    builder.HasIndex(entry => new
    {
      entry.CustomerId,
      entry.CreatedAt
    })
        .HasDatabaseName("ix_ledger_entries_customer_created_at");
  }
}
