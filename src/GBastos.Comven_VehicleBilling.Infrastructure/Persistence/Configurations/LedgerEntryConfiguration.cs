using Comven_VehicleBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration
    : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
    builder.ToTable(
        "ledger_entries",
        table => table.HasCheckConstraint(
            "ck_ledger_entries_exactly_one_origin",
            """
        (
            "operation_id" IS NOT NULL
            AND "balance_credit_id" IS NULL
            AND "type" IN (1, 2)
        )
        OR
        (
            "operation_id" IS NULL
            AND "balance_credit_id" IS NOT NULL
            AND "type" = 2
        )
        """));

    builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(entry => entry.OperationId)
            .HasColumnName("operation_id")
            .IsRequired(false);

        builder.Property(entry => entry.BalanceCreditId)
            .HasColumnName("balance_credit_id")
            .IsRequired(false);

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

        builder.HasOne<BalanceCredit>()
            .WithMany()
            .HasForeignKey(entry => entry.BalanceCreditId)
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
            .HasFilter("\"operation_id\" IS NOT NULL")
            .HasDatabaseName("ux_ledger_entries_operation_type");

        builder.HasIndex(entry => new
        {
            entry.BalanceCreditId,
            entry.Type
        })
            .IsUnique()
            .HasFilter("\"balance_credit_id\" IS NOT NULL")
            .HasDatabaseName("ux_ledger_entries_balance_credit_type");
    }
}
