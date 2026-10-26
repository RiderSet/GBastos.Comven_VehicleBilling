namespace Comven_VehicleBilling.Infrastructure.Inbox;

public sealed class InboxMessage
{
    public Guid Id { get; private set; }

    public string Consumer { get; private set; } = string.Empty;

    public DateTime ProcessedAt { get; private set; }

    private InboxMessage()
    {
        // Entity Framework Core.
    }

    public InboxMessage(Guid id, string consumer)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "O identificador da mensagem não pode ser vazio.",
                nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        Id = id;
        Consumer = consumer;
        ProcessedAt = DateTime.UtcNow;
    }
}
