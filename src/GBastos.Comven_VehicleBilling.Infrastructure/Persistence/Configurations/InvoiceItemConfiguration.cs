using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class InvoiceItemConfiguration
    : IEntityTypeConfiguration<InvoiceItem>
{
  public void Configure(EntityTypeBuilder<InvoiceItem> builder)
  {
    builder.ToTable("invoice_items");

    builder.HasKey(item => new
    {
      item.InvoiceId,
      item.OperationId
    });

    builder.Property(item => item.InvoiceId)
        .HasColumnName("invoice_id")
        .IsRequired();

    builder.Property(item => item.OperationId)
        .HasColumnName("operation_id")
        .IsRequired();

    builder.Property(item => item.Amount)
        .HasColumnName("amount")
        .HasConversion(
            money => money.Amount,
            amount => Money.Of(amount))
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.HasOne(item => item.Invoice)
        .WithMany(invoice => invoice.Items)
        .HasForeignKey(item => item.InvoiceId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Operation>()
        .WithMany()
        .HasForeignKey(item => item.OperationId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(item => item.OperationId)
        .IsUnique()
        .HasDatabaseName("ux_invoice_items_operation_id");
  }
}
