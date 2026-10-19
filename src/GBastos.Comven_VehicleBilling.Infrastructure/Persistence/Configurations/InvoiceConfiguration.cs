using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration
    : IEntityTypeConfiguration<Invoice>
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

    // Uma única fatura por cliente e período.
    builder.HasIndex(invoice => new
    {
      invoice.CustomerId,
      invoice.PeriodStart,
      invoice.PeriodEnd
    })
        .IsUnique()
        .HasDatabaseName("ux_invoices_customer_period");

    builder.HasIndex(invoice => new
    {
      invoice.Status,
      invoice.CreatedAt
    })
        .HasDatabaseName("ix_invoices_status_created_at");
  }
}
