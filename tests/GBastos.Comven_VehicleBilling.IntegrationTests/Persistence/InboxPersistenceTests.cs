using Comven_VehicleBilling.Infrastructure.Inbox;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.IntegrationTests.Persistence;

[Collection("PostgreSQL integration tests")]
public sealed class InboxPersistenceTests
{
    private static VehicleBillingDbContext CreateDbContext()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Defina VEHICLE_BILLING_TEST_CONNECTION_STRING.");

        var options =
            new DbContextOptionsBuilder<VehicleBillingDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new VehicleBillingDbContext(options);
    }

    [Fact]
    public async Task ShouldRegisterMessageOnlyOncePerConsumer()
    {
        var messageId = Guid.NewGuid();
        const string consumer = "inbox-integration-test";

        try
        {
            await using var dbContext = CreateDbContext();

            var repository = new InboxRepository(dbContext);

            var firstRegistration = await repository.TryRegisterAsync(
                messageId, consumer, CancellationToken.None);

            var duplicateRegistration = await repository.TryRegisterAsync(
                messageId, consumer, CancellationToken.None);

            var anotherConsumerRegistration = await repository.TryRegisterAsync(
                messageId, "another-consumer", CancellationToken.None);

            Assert.True(firstRegistration);
            Assert.False(duplicateRegistration);
            Assert.True(anotherConsumerRegistration);
        }
        finally
        {
            await using var cleanupContext = CreateDbContext();

            await cleanupContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM inbox_messages WHERE id = {messageId}");
        }
    }

    [Fact]
    public async Task ShouldAllowRegistrationAgainAfterTransactionRollback()
    {
        var messageId = Guid.NewGuid();
        const string consumer = "inbox-rollback-test";

        try
        {
            await using (var dbContext = CreateDbContext())
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync();

                var repository = new InboxRepository(dbContext);

                var registered = await repository.TryRegisterAsync(
                    messageId, consumer, CancellationToken.None);

                Assert.True(registered);

                await transaction.RollbackAsync();
            }

            await using (var verificationContext = CreateDbContext())
            {
                var repository = new InboxRepository(verificationContext);

                var registeredAgain = await repository.TryRegisterAsync(
                    messageId, consumer, CancellationToken.None);

                Assert.True(registeredAgain);
            }
        }
        finally
        {
            await using var cleanupContext = CreateDbContext();

            await cleanupContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM inbox_messages WHERE id = {messageId}");
        }
    }
}

