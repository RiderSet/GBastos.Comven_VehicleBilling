using Comven_VehicleBilling.Application.Operations;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Comven_VehicleBilling.IntegrationTests.Persistence;

public sealed class OperationIdempotencyTests
{
    private static VehicleBillingDbContext CreateDbContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Defina VEHICLE_BILLING_TEST_CONNECTION_STRING.");
        }

        var options = new DbContextOptionsBuilder<VehicleBillingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new VehicleBillingDbContext(options);
    }

    private static async Task<Guid> CreateCustomerAsync()
    {
        var customerId = Guid.NewGuid();

        var customer = new Customer(
            customerId,
            $"Idempotency Test {customerId:N}",
            Enum.GetValues<CustomerPlan>().First(),
            Money.Zero,
            DateTimeOffset.UtcNow);

        await using var db = CreateDbContext();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return customerId;
    }

    private static CreateOperationCommand NewCommand(
        Guid externalId,
        Guid customerId,
        decimal amount = 10m)
    {
        var operationType = Enum.GetValues<OperationType>().First();

        return new CreateOperationCommand(
            externalId,
            customerId,
            operationType.ToString(),
            amount);
    }

    private static async Task CleanupAsync(
        Guid externalId,
        Guid customerId)
    {
        await using var db = CreateDbContext();

        var operations = await db.Operations
            .Where(x => x.ExternalOperationId == externalId)
            .ToListAsync();

        db.Operations.RemoveRange(operations);

        var customer = await db.Customers
            .SingleOrDefaultAsync(x => x.Id == customerId);

        if (customer is not null)
            db.Customers.Remove(customer);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Repeated_operation_with_same_external_id_must_not_duplicate()
    {
        var customerId = await CreateCustomerAsync();
        var externalId = Guid.NewGuid();

        try
        {
            var command = NewCommand(externalId, customerId);

            OperationDto first;
            await using (var db = CreateDbContext())
            {
                first = await new OperationRepository(db)
                    .CreateAsync(command);
            }

            OperationDto second;
            await using (var db = CreateDbContext())
            {
                second = await new OperationRepository(db)
                    .CreateAsync(command);
            }

            Assert.Equal(first.Id, second.Id);

            await using var verifyDb = CreateDbContext();
            var count = await verifyDb.Operations.CountAsync(
                x => x.ExternalOperationId == externalId);

            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupAsync(externalId, customerId);
        }
    }

    [Fact]
    public async Task Reusing_external_id_with_different_data_must_be_rejected()
    {
        var customerId = await CreateCustomerAsync();
        var externalId = Guid.NewGuid();

        try
        {
            await using (var db = CreateDbContext())
            {
                await new OperationRepository(db).CreateAsync(
                    NewCommand(externalId, customerId, 10m));
            }

            await using (var db = CreateDbContext())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => new OperationRepository(db).CreateAsync(
                        NewCommand(externalId, customerId, 25m)));
            }

            await using var verifyDb = CreateDbContext();
            var operations = await verifyDb.Operations
                .Where(x => x.ExternalOperationId == externalId)
                .ToListAsync();

            Assert.Single(operations);
            Assert.Equal(10m, operations[0].Amount.Amount);
        }
        finally
        {
            await CleanupAsync(externalId, customerId);
        }
    }

    [Fact]
    public async Task Concurrent_requests_with_same_external_id_must_not_duplicate()
    {
        var customerId = await CreateCustomerAsync();
        var externalId = Guid.NewGuid();

        try
        {
            var command = NewCommand(externalId, customerId);

            async Task<OperationDto> ExecuteAsync()
            {
                await using var db = CreateDbContext();
                return await new OperationRepository(db).CreateAsync(command);
            }

            var results = await Task.WhenAll(
                ExecuteAsync(),
                ExecuteAsync());

            Assert.Equal(results[0].Id, results[1].Id);

            await using var verifyDb = CreateDbContext();
            var count = await verifyDb.Operations.CountAsync(
                x => x.ExternalOperationId == externalId);

            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupAsync(externalId, customerId);
        }
    }
}
