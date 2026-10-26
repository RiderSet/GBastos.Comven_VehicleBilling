using Comven_VehicleBilling.Infrastructure.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration
    : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(message => new
        {
            message.Id,
            message.Consumer
        });

        builder.Property(message => message.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(message => message.Consumer)
            .HasMaxLength(200)
            .IsRequired()
            .HasColumnName("consumer");

        builder.Property(message => message.ProcessedAt)
            .IsRequired()
            .HasColumnName("processed_at");

        builder.HasIndex(message => message.ProcessedAt)
            .HasDatabaseName("ix_inbox_messages_processed_at");
    }
}
