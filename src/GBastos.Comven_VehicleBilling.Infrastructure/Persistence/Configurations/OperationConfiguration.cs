using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class OperationConfiguration
    : IEntityTypeConfiguration<Operation>
{
  public void Configure(EntityTypeBuilder<Operation> builder)
  {
    builder.ToTable("operations");

    builder.HasKey(operation => operation.Id);

    builder.Property(operation => operation.Id)
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(operation => operation.CustomerId)
        .HasColumnName("customer_id")
        .IsRequired();

    builder.Property(operation => operation.Type)
        .HasColumnName("type")
        .HasConversion<int>()
        .IsRequired();

    builder.Property(operation => operation.Amount)
        .HasColumnName("amount")
        .HasConversion(
            money => money.Amount,
            amount => Money.Of(amount))
        .HasColumnType("numeric(18,2)")
        .IsRequired();

    builder.Property(operation => operation.Status)
        .HasColumnName("status")
        .HasConversion<int>()
        .IsRequired();

    builder.Property(operation => operation.RequestHash)
        .HasColumnName("request_hash")
        .HasMaxLength(200)
        .IsRequired();

    builder.Property(operation => operation.CreatedAt)
        .HasColumnName("created_at")
        .IsRequired();

    builder.Property(operation => operation.InvoiceId)
        .HasColumnName("invoice_id");

    builder.HasOne(operation => operation.Customer)
        .WithMany()
        .HasForeignKey(operation => operation.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(operation => operation.Invoice)
        .WithMany()
        .HasForeignKey(operation => operation.InvoiceId)
        .OnDelete(DeleteBehavior.Restrict);

    // A mesma chave de solicitação não pode ser reutilizada
    // pelo mesmo cliente para criar operações duplicadas.
    builder.HasIndex(operation => new
    {
      operation.CustomerId,
      operation.RequestHash
    })
        .IsUnique()
        .HasDatabaseName("ux_operations_customer_request_hash");

    builder.HasIndex(operation => new
    {
      operation.CustomerId,
      operation.CreatedAt
    })
        .HasDatabaseName("ix_operations_customer_created_at");

    builder.HasIndex(operation => new
    {
      operation.Status,
      operation.CreatedAt
    })
        .HasDatabaseName("ix_operations_status_created_at");

    builder.HasIndex(operation => operation.InvoiceId)
        .HasDatabaseName("ix_operations_invoice_id");
  }
}
