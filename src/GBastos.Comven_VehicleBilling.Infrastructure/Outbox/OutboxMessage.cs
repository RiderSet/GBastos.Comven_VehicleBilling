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

  public Guid? LockId { get; private set; }
  public DateTime? LockedUntil { get; private set; }

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

  public bool IsLocked(DateTime now) =>
      LockId.HasValue &&
      LockedUntil.HasValue &&
      LockedUntil.Value > now;

  public void AcquireLock(Guid lockId, DateTime lockedUntil)
  {
    if (IsProcessed)
    {
      throw new InvalidOperationException(
          "A mensagem já foi processada.");
    }

    if (lockId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador da reserva não pode ser vazio.",
          nameof(lockId));
    }

    if (lockedUntil <= DateTime.UtcNow)
    {
      throw new ArgumentOutOfRangeException(
          nameof(lockedUntil),
          "A expiração da reserva deve estar no futuro.");
    }

    if (IsLocked(DateTime.UtcNow))
    {
      throw new InvalidOperationException(
          "A mensagem já possui uma reserva válida.");
    }

    LockId = lockId;
    LockedUntil = lockedUntil;
  }

  public void MarkAsProcessed(Guid lockId)
  {
    EnsureLockOwner(lockId);

    ProcessedAt = DateTime.UtcNow;
    NextAttemptAt = null;
    LastError = null;
    ReleaseLock();
  }

  public void RegisterFailure(
      Guid lockId,
      string error,
      DateTime? nextAttemptAt = null)
  {
    EnsureLockOwner(lockId);

    ArgumentException.ThrowIfNullOrWhiteSpace(error);

    AttemptCount++;
    LastError = error;
    NextAttemptAt = nextAttemptAt;
    ReleaseLock();
  }

  private void EnsureLockOwner(Guid lockId)
  {
    if (IsProcessed)
    {
      throw new InvalidOperationException(
          "A mensagem já foi processada.");
    }

    if (lockId == Guid.Empty ||
        LockId != lockId ||
        !LockedUntil.HasValue ||
        LockedUntil.Value <= DateTime.UtcNow)
    {
      throw new InvalidOperationException(
          "A reserva da mensagem é inválida ou expirou.");
    }
  }

  private void ReleaseLock()
  {
    LockId = null;
    LockedUntil = null;
  }
}
