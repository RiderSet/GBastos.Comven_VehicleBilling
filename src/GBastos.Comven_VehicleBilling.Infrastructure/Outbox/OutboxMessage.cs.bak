namespace Comven_VehicleBilling.Infrastructure.Outbox;

public sealed class OutboxMessage
{
  public Guid Id { get; private set; }
  public string MessageType { get; private set; } = string.Empty;
  public Guid? AggregateId { get; private set; }
  public string Payload { get; private set; } = string.Empty;
  public DateTime CreatedAt { get; private set; }
  public DateTime? ProcessedAt { get; private set; }
  public int AttemptCount { get; private set; }
  public DateTime? NextAttemptAt { get; private set; }
  public string? LastError { get; private set; }

  private OutboxMessage()
  {
    // Entity Framework Core.
  }

  public OutboxMessage(
      string messageType,
      Guid? aggregateId,
      string payload)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
    ArgumentException.ThrowIfNullOrWhiteSpace(payload);

    Id = Guid.NewGuid();
    MessageType = messageType;
    AggregateId = aggregateId;
    Payload = payload;
    CreatedAt = DateTime.UtcNow;
  }

  public bool IsProcessed => ProcessedAt.HasValue;

  public void MarkAsProcessed()
  {
    ProcessedAt = DateTime.UtcNow;
    NextAttemptAt = null;
    LastError = null;
  }

  public void RegisterFailure(
      string error,
      DateTime? nextAttemptAt = null)
  {
    if (IsProcessed)
    {
      throw new InvalidOperationException(
          "A mensagem já foi processada.");
    }

    ArgumentException.ThrowIfNullOrWhiteSpace(error);

    AttemptCount++;
    LastError = error;
    NextAttemptAt = nextAttemptAt;
  }
}
