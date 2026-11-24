using Comven_VehicleBilling.Infrastructure.PaymentsProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class PaymentProviderTransactionConfiguration
    : IEntityTypeConfiguration<PaymentProviderTransaction>
{
  public void Configure(
      EntityTypeBuilder<PaymentProviderTransaction> builder)
  {
    builder.ToTable("payment_provider_transactions");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(x => x.IdempotencyKey)
        .HasColumnName("idempotency_key")
        .HasMaxLength(32)
        .IsRequired();

    builder.HasIndex(x => x.IdempotencyKey)
        .IsUnique()
        .HasDatabaseName("ux_payment_provider_transactions_idempotency_key");

    builder.Property(x => x.InvoiceId)
        .HasColumnName("invoice_id")
        .IsRequired();

    builder.HasOne<Comven_VehicleBilling.Domain.Entities.Invoice>()
        .WithMany()
        .HasForeignKey(x => x.InvoiceId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.Property(x => x.CustomerId)
        .HasColumnName("customer_id")
        .IsRequired();

    builder.Property(x => x.Amount)
        .HasColumnName("amount")
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.Property(x => x.Outcome)
        .HasColumnName("outcome")
        .HasConversion<int>()
        .IsRequired();

    builder.Property(x => x.ExternalPaymentId)
        .HasColumnName("external_payment_id")
        .HasMaxLength(200);

    builder.Property(x => x.ErrorMessage)
        .HasColumnName("error_message")
        .HasMaxLength(1000);

    builder.Property(x => x.CreatedAt)
        .HasColumnName("created_at")
        .IsRequired();
  }
}
