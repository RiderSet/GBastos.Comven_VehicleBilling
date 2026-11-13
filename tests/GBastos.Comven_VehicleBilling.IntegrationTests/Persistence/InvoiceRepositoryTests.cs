using Comven_VehicleBilling.Application.Invoices;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Comven_VehicleBilling.IntegrationTests.Persistence;

[Collection("PostgreSQL integration tests")]
public sealed class InvoiceRepositoryTests
{
    [Fact]
    public async Task CreateAsync_ShouldInvoiceOnlyOperationsInsideReferenceMonth_AndBeIdempotent()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure VEHICLE_BILLING_TEST_CONNECTION_STRING.");

        var connection = new Npgsql.NpgsqlConnectionStringBuilder(
            connectionString);

        Assert.Equal(
            "vehicle_billing_test",
            connection.Database);

        var options = new DbContextOptionsBuilder<VehicleBillingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var customerId = Guid.NewGuid();

        var octoberOperation1 = Guid.NewGuid();
        var octoberOperation2 = Guid.NewGuid();
        var septemberOperation = Guid.NewGuid();
        var novemberOperation = Guid.NewGuid();

        Guid? invoiceId = null;

        await using var db = new VehicleBillingDbContext(options);

        try
        {
            var customer = new Customer(
                customerId,
                $"Invoice Integration Test {customerId:N}",
                CustomerPlan.Postpaid,
                Money.Zero,
                new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

            db.Customers.Add(customer);

            AddAwaitingInvoiceOperation(
                db, octoberOperation1, customerId,
                OperationType.VehicleConsultation,
                12.50m,
                new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

            AddAwaitingInvoiceOperation(
                db, octoberOperation2, customerId,
                OperationType.CrlveIssuance,
                45.00m,
                new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

            AddAwaitingInvoiceOperation(
                db, septemberOperation, customerId,
                OperationType.PlateConsultation,
                8.90m,
                new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

            AddAwaitingInvoiceOperation(
                db, novemberOperation, customerId,
                OperationType.PlateConsultation,
                8.90m,
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

            await db.SaveChangesAsync();

            var repository = new InvoiceRepository(db);

            var command = new CreateInvoiceCommand(
                customerId,
                new DateOnly(2026, 10, 15));

            var result = await repository.CreateAsync(command);

            invoiceId = result.Id;

            Assert.Equal(customerId, result.CustomerId);
            Assert.Equal(new DateOnly(2026, 10, 1), result.PeriodStart);
            Assert.Equal(new DateOnly(2026, 11, 1), result.PeriodEnd);
            Assert.Equal(57.50m, result.Amount);

            var operations = await db.Operations
                .Where(x => x.CustomerId == customerId)
                .ToDictionaryAsync(x => x.Id);

            Assert.Equal(
                OperationStatus.Invoiced,
                operations[octoberOperation1].Status);

            Assert.Equal(
                OperationStatus.Invoiced,
                operations[octoberOperation2].Status);

            Assert.Equal(
                OperationStatus.AwaitingInvoice,
                operations[septemberOperation].Status);

            Assert.Equal(
                OperationStatus.AwaitingInvoice,
                operations[novemberOperation].Status);

            var invoice = await db.Invoices
                .Include(x => x.Items)
                .SingleAsync(x => x.Id == result.Id);

            Assert.Equal(2, invoice.Items.Count);

            Assert.Contains(
                invoice.Items,
                x => x.OperationId == octoberOperation1);

            Assert.Contains(
                invoice.Items,
                x => x.OperationId == octoberOperation2);

            var secondResult = await repository.CreateAsync(command);

            Assert.Equal(result.Id, secondResult.Id);

            var invoiceCount = await db.Invoices
                .CountAsync(x => x.CustomerId == customerId);

            Assert.Equal(1, invoiceCount);
        }
        finally
        {
            if (invoiceId.HasValue)
            {
                await db.OutboxMessages
                    .Where(x => x.AggregateId == invoiceId.Value)
                    .ExecuteDeleteAsync();
            }

            await db.InvoiceItems
                .Where(x => x.Invoice.CustomerId == customerId)
                .ExecuteDeleteAsync();

            await db.Operations
                .Where(x => x.CustomerId == customerId)
                .ExecuteDeleteAsync();

            await db.Invoices
                .Where(x => x.CustomerId == customerId)
                .ExecuteDeleteAsync();

            await db.Customers
                .Where(x => x.Id == customerId)
                .ExecuteDeleteAsync();
        }
    }

    private static void AddAwaitingInvoiceOperation(
        VehicleBillingDbContext db,
        Guid operationId,
        Guid customerId,
        OperationType type,
        decimal amount,
        DateTimeOffset createdAt)
    {
        var operation = new Operation(
            operationId,
            Guid.NewGuid(),
            customerId,
            type,
            Money.Of(amount),
            Guid.NewGuid().ToString("N"),
            createdAt);

        operation.MarkAwaitingInvoice();

        db.Operations.Add(operation);
    }
}

