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
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(entry => entry.CustomerId)
        .HasColumnName("customer_id")
        .IsRequired();

    builder.Property(entry => entry.OperationId)
        .HasColumnName("operation_id")
        .IsRequired();

    builder.Property(entry => entry.Type)
        .HasColumnName("type")
        .HasConversion<int>()
        .IsRequired();

    builder.Property(entry => entry.Description)
        .HasColumnName("description")
        .HasMaxLength(250)
        .IsRequired();

    builder.Property(entry => entry.CreatedAt)
        .HasColumnName("created_at")
        .IsRequired();

    builder.Property(entry => entry.Amount)
        .HasConversion(
            money => money.Amount,
            amount => Comven_VehicleBilling.Domain.ValueObjects.Money.Of(amount))
        .HasColumnName("amount")
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.HasOne(entry => entry.Customer)
        .WithMany()
        .HasForeignKey(entry => entry.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Operation>()
        .WithMany()
        .HasForeignKey(entry => entry.OperationId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(entry => new
    {
      entry.CustomerId,
      entry.CreatedAt
    })
        .HasDatabaseName("ix_ledger_entries_customer_created_at");

    builder.HasIndex(entry => new
    {
      entry.OperationId,
      entry.Type
    })
        .IsUnique()
        .HasDatabaseName("ux_ledger_entries_operation_type");
  }
}
