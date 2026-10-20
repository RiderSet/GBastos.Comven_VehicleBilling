namespace Comven_VehicleBilling.Domain.Entities;

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
    // Necessário para o Entity Framework Core.
  }

  public OutboxMessage(
      string messageType,
      Guid? aggregateId,
      string payload)
  {
    if (string.IsNullOrWhiteSpace(messageType))
      throw new ArgumentException(
          "O tipo da mensagem é obrigatório.",
          nameof(messageType));

    if (string.IsNullOrWhiteSpace(payload))
      throw new ArgumentException(
          "O conteúdo da mensagem é obrigatório.",
          nameof(payload));

    Id = Guid.NewGuid();
    MessageType = messageType;
    AggregateId = aggregateId;
    Payload = payload;
    CreatedAt = DateTime.UtcNow;
    AttemptCount = 0;
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
      throw new InvalidOperationException(
          "Uma mensagem processada não pode registrar falha.");

    AttemptCount++;
    LastError = error;
    NextAttemptAt = nextAttemptAt;
  }
}
