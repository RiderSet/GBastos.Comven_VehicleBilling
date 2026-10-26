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
      await using (var dbContext = CreateDbContext())
      {
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync();
      }

      await using var verificationContext = CreateDbContext();

      var persistedMessage =
          await verificationContext.OutboxMessages
              .AsNoTracking()
              .SingleOrDefaultAsync(
                  item => item.Id == message.Id);

      Assert.NotNull(persistedMessage);

      Assert.Equal(message.Id, persistedMessage.Id);
      Assert.Equal(
          "IntegrationTest.MessageCreated",
          persistedMessage.MessageType);
      Assert.Equal(
          message.AggregateId,
          persistedMessage.AggregateId);

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

  [Fact]
  public async Task ShouldReserveDistinctMessagesWhenCalledConcurrently()
  {
    const int batchSize = 5;
    const int messageCount = batchSize * 2;

    var messages = Enumerable.Range(0, messageCount)
        .Select(index => new OutboxMessage(
            messageType: "IntegrationTest.ConcurrentReservation",
            aggregateId: Guid.NewGuid(),
            payload: JsonSerializer.Serialize(
                new { source = "concurrency-test", index })))
        .ToArray();

    var expectedIds = messages
        .Select(message => message.Id)
        .ToHashSet();

    try
    {
      // Insere somente mensagens pertencentes a este teste.
      // A data antiga garante prioridade sobre mensagens recentes.
      await using (var dbContext = CreateDbContext())
      {
        for (var index = 0; index < messages.Length; index++)
        {
          dbContext.Entry(messages[index])
              .Property("CreatedAt")
              .CurrentValue = DateTime.UtcNow
                  .AddYears(-100)
                  .AddMilliseconds(index);
        }

        dbContext.OutboxMessages.AddRange(messages);

        await dbContext.SaveChangesAsync();
      }

      // Cada chamada usa seu próprio DbContext e sua própria transação.
      var tasks = Enumerable.Range(0, 2)
          .Select(async _ =>
          {
            await using var dbContext = CreateDbContext();

            var repository = new OutboxRepository(dbContext);

            return await repository.GetPendingAsync(batchSize);
          })
          .ToArray();

      var batches = await Task.WhenAll(tasks);

      var firstIds = batches[0]
          .Select(message => message.Id)
          .ToHashSet();

      var secondIds = batches[1]
          .Select(message => message.Id)
          .ToHashSet();

      // Cada chamada deve reservar cinco mensagens.
      Assert.Equal(batchSize, firstIds.Count);
      Assert.Equal(batchSize, secondIds.Count);

      // Nenhuma mensagem pode pertencer aos dois lotes.
      Assert.Empty(firstIds.Intersect(secondIds));

      // Os dois lotes devem conter exatamente as dez mensagens do teste.
      Assert.True(expectedIds.SetEquals(firstIds.Concat(secondIds)));
    }
    finally
    {
      // Limpa exclusivamente as mensagens criadas por este teste.
      await using var cleanupContext = CreateDbContext();

      var createdMessages =
          await cleanupContext.OutboxMessages
              .Where(message => expectedIds.Contains(message.Id))
              .ToListAsync();

      if (createdMessages.Count > 0)
      {
        cleanupContext.OutboxMessages.RemoveRange(createdMessages);
        await cleanupContext.SaveChangesAsync();
      }
    }

  }
}
