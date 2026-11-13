using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
  public void Configure(EntityTypeBuilder<Invoice> builder)
  {
    builder.ToTable("invoices");

    builder.HasKey(invoice => invoice.Id);

    builder.Property(invoice => invoice.Id)
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(invoice => invoice.CustomerId)
        .HasColumnName("customer_id")
        .IsRequired();

    builder.HasOne<Invoice>()
        .WithMany()
        .HasForeignKey(invoice => invoice.OriginalInvoiceId)
        .HasConstraintName("fk_invoices_original_invoice")
        .OnDelete(DeleteBehavior.Restrict);

    builder.Property(invoice => invoice.OriginalInvoiceId)
        .HasColumnName("original_invoice_id")
        .IsRequired(false);

    builder.Property(invoice => invoice.IdempotencyKey)
        .HasColumnName("idempotency_key")
        .HasMaxLength(200)
        .IsRequired(false);

    builder.HasIndex(invoice => new
    {
        invoice.OriginalInvoiceId,
        invoice.IdempotencyKey
    })
        .IsUnique()
        .HasDatabaseName("ux_invoices_original_idempotency")
        .HasFilter("original_invoice_id IS NOT NULL AND idempotency_key IS NOT NULL");

    builder.Property(invoice => invoice.PeriodStart)
            .HasColumnName("period_start")
            .HasColumnType("date")
            .IsRequired();

    builder.Property(invoice => invoice.PeriodEnd)
        .HasColumnName("period_end")
        .HasColumnType("date")
        .IsRequired();

    builder.Property(invoice => invoice.Total)
        .HasColumnName("total")
        .HasConversion(
            money => money.Amount,
            amount => Money.Of(amount))
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.Property(invoice => invoice.Status)
        .HasColumnName("status")
        .HasConversion<int>()
        .IsRequired();

    builder.Property(invoice => invoice.CreatedAt)
        .HasColumnName("created_at")
        .IsRequired();

    builder.HasOne(invoice => invoice.Customer)
        .WithMany()
        .HasForeignKey(invoice => invoice.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(invoice => invoice.Items)
        .WithOne(item => item.Invoice)
        .HasForeignKey(item => item.InvoiceId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.Navigation(invoice => invoice.Items)
        .UsePropertyAccessMode(PropertyAccessMode.Field);

    builder.HasIndex(invoice => new
    {
      invoice.Status,
      invoice.CreatedAt
    })
        .HasDatabaseName("ix_invoices_status_created_at");
  }
}
