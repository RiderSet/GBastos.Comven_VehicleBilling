using Comven_VehicleBilling.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.MessageType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.AggregateId);

        builder.Property(message => message.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .IsRequired();

        builder.Property(message => message.ProcessedAt);

        builder.Property(message => message.AttemptCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(message => message.NextAttemptAt);

        builder.Property(message => message.LastError)
            .HasColumnType("text");

        builder.Property(message => message.LockId);

        builder.Property(message => message.LockedUntil);

        builder.HasIndex(message => new
        {
            message.ProcessedAt,
            message.NextAttemptAt,
            message.CreatedAt
        })
        .HasDatabaseName("ix_outbox_messages_pending");
    }
}
