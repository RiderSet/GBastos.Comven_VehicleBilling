using Comven_VehicleBilling.Infrastructure.Outbox;

namespace Comven_VehicleBilling.UnitTests.Outbox;

public sealed class OutboxMessageTests
{
  [Fact]
  public void RegisterFailure_ShouldIncrementAttemptCountAndScheduleRetry()
  {
    // Arrange
    var message = new OutboxMessage(
        "Test.Message",
        Guid.NewGuid(),
        """{"test":true}""");

    var nextAttemptAt = DateTime.UtcNow.AddSeconds(30);

    // Act
    message.RegisterFailure("Falha simulada", nextAttemptAt);

    // Assert
    Assert.Equal(1, message.AttemptCount);
    Assert.Equal("Falha simulada", message.LastError);
    Assert.Equal(nextAttemptAt, message.NextAttemptAt);
    Assert.Null(message.ProcessedAt);
    Assert.False(message.IsProcessed);
  }

  [Fact]
  public void MarkAsProcessed_ShouldClearRetryInformation()
  {
    // Arrange
    var message = new OutboxMessage(
        "Test.Message",
        Guid.NewGuid(),
        """{"test":true}""");

    message.RegisterFailure(
        "Falha simulada",
        DateTime.UtcNow.AddSeconds(30));

    // Act
    message.MarkAsProcessed();

    // Assert
    Assert.True(message.IsProcessed);
    Assert.NotNull(message.ProcessedAt);
    Assert.Null(message.NextAttemptAt);
    Assert.Null(message.LastError);
  }

  [Fact]
  public void RegisterFailure_ShouldRejectAlreadyProcessedMessage()
  {
    // Arrange
    var message = new OutboxMessage(
        "Test.Message",
        Guid.NewGuid(),
        """{"test":true}""");

    message.MarkAsProcessed();

    // Act
    var exception = Assert.Throws<InvalidOperationException>(
        () => message.RegisterFailure("Falha simulada"));

    // Assert
    Assert.Equal("A mensagem já foi processada.", exception.Message);
  }
}
