using Comven_VehicleBilling.Infrastructure.Outbox;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Comven_VehicleBilling.IntegrationTests.Persistence;

public sealed class OutboxPersistenceTests
{
  private static VehicleBillingDbContext CreateDbContext()
  {
    var connectionString =
        Environment.GetEnvironmentVariable(
            "VEHICLE_BILLING_TEST_CONNECTION_STRING");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException(
          "Defina VEHICLE_BILLING_TEST_CONNECTION_STRING " +
          "com a connection string do PostgreSQL.");
    }

    var options =
        new DbContextOptionsBuilder<VehicleBillingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

    return new VehicleBillingDbContext(options);
  }

  [Fact]
  public async Task ShouldPersistAndRetrieveOutboxMessage()
  {
    var message = new OutboxMessage(
        messageType: "IntegrationTest.MessageCreated",
        aggregateId: Guid.NewGuid(),
        payload: """{"source":"integration-test"}""");

    try
    {
      // Arrange e Act: persiste a mensagem.
      await using (var dbContext = CreateDbContext())
      {
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync();
      }

      // Recupera a mensagem usando um novo contexto.
      await using var verificationContext = CreateDbContext();

      var persistedMessage =
          await verificationContext.OutboxMessages
              .AsNoTracking()
              .SingleOrDefaultAsync(
                  item => item.Id == message.Id);

      // Assert: verifica se a mensagem foi persistida.
      Assert.NotNull(persistedMessage);

      Assert.Equal(message.Id, persistedMessage.Id);
      Assert.Equal(
          "IntegrationTest.MessageCreated",
          persistedMessage.MessageType);
      Assert.Equal(
          message.AggregateId,
          persistedMessage.AggregateId);

      // Compara o conteúdo JSON, não sua formatação textual.
      using var payloadDocument =
          JsonDocument.Parse(persistedMessage.Payload);

      Assert.Equal(
          "integration-test",
          payloadDocument.RootElement
              .GetProperty("source")
              .GetString());

      Assert.Null(persistedMessage.ProcessedAt);
      Assert.Equal(0, persistedMessage.AttemptCount);
    }
    finally
    {
      // Remove exclusivamente o registro criado por este teste.
      await using var cleanupContext = CreateDbContext();

      var createdMessage =
          await cleanupContext.OutboxMessages
              .SingleOrDefaultAsync(
                  item => item.Id == message.Id);

      if (createdMessage is not null)
      {
        cleanupContext.OutboxMessages.Remove(createdMessage);
        await cleanupContext.SaveChangesAsync();
      }
    }
  }
}
