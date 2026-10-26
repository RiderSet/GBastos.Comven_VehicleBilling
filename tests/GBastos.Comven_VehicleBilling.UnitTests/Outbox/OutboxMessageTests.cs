using Comven_VehicleBilling.Infrastructure.Outbox;

namespace Comven_VehicleBilling.UnitTests.Outbox;

public sealed class OutboxMessageTests
{
  private static OutboxMessage CreateMessage() =>
      new(
          "Test.Message",
          Guid.NewGuid(),
          """{"test":true}""");

  private static Guid AcquireLock(OutboxMessage message)
  {
    var lockId = Guid.NewGuid();

    message.AcquireLock(
        lockId,
        DateTime.UtcNow.AddMinutes(5));

    return lockId;
  }

  [Fact]
  public void RegisterFailure_ShouldIncrementAttemptCountAndScheduleRetry()
  {
    var message = CreateMessage();
    var lockId = AcquireLock(message);
    var nextAttemptAt = DateTime.UtcNow.AddSeconds(30);

    message.RegisterFailure(
        lockId,
        "Falha simulada",
        nextAttemptAt);

    Assert.Equal(1, message.AttemptCount);
    Assert.Equal("Falha simulada", message.LastError);
    Assert.Equal(nextAttemptAt, message.NextAttemptAt);
    Assert.Null(message.ProcessedAt);
    Assert.False(message.IsProcessed);
    Assert.Null(message.LockId);
    Assert.Null(message.LockedUntil);
  }

  [Fact]
  public void MarkAsProcessed_ShouldClearRetryInformation()
  {
    var message = CreateMessage();
    var failureLockId = AcquireLock(message);

    message.RegisterFailure(
        failureLockId,
        "Falha simulada",
        DateTime.UtcNow.AddSeconds(30));

    var successLockId = AcquireLock(message);

    message.MarkAsProcessed(successLockId);

    Assert.True(message.IsProcessed);
    Assert.NotNull(message.ProcessedAt);
    Assert.Null(message.NextAttemptAt);
    Assert.Null(message.LastError);
    Assert.Null(message.LockId);
    Assert.Null(message.LockedUntil);
  }

  [Fact]
  public void RegisterFailure_ShouldRejectAlreadyProcessedMessage()
  {
    var message = CreateMessage();
    var lockId = AcquireLock(message);

    message.MarkAsProcessed(lockId);

    var exception = Assert.Throws<InvalidOperationException>(
        () => message.RegisterFailure(
            lockId,
            "Falha simulada"));

    Assert.Equal(
        "A mensagem já foi processada.",
        exception.Message);
  }

  [Fact]
  public void AcquireLock_ShouldRejectAnotherActiveLock()
  {
    var message = CreateMessage();
    AcquireLock(message);

    Assert.Throws<InvalidOperationException>(
        () => message.AcquireLock(
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(5)));
  }

  [Fact]
  public void MarkAsProcessed_ShouldRejectIncorrectLockId()
  {
    var message = CreateMessage();
    AcquireLock(message);

    Assert.Throws<InvalidOperationException>(
        () => message.MarkAsProcessed(Guid.NewGuid()));
  }
}
