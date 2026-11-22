using Comven_VehicleBilling.Application.Invoices;
using Comven_VehicleBilling.Application.Abstractions.Payments;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Infrastructure.Persistence.Repositories;
using Comven_VehicleBilling.Infrastructure.Outbox;
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

            var testClock = new FixedTimeProvider(
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

            var repository = new InvoiceRepository(db, testClock);

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


    [Fact]
    public async Task StartPaymentAsync_ShouldChangeStatusToProcessing()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<VehicleBillingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        await using var db = new VehicleBillingDbContext(options);

        try
        {
            await CreatePaymentTestDataAsync(
                db, customerId, invoiceId, operationId);

            var repository = new InvoiceRepository(
                db,
                TimeProvider.System);

            var result = await repository.StartPaymentAsync(invoiceId);

            Assert.NotNull(result);
            Assert.Equal("PaymentProcessing", result.Status);

            var persistedInvoice = await db.Invoices
                .AsNoTracking()
                .SingleAsync(x => x.Id == invoiceId);

            Assert.Equal(
                InvoiceStatus.PaymentProcessing,
                persistedInvoice.Status);

            var secondAttempt =
                await repository.StartPaymentAsync(invoiceId);

            Assert.Null(secondAttempt);

            Assert.Empty(await db.OutboxMessages
                .Where(x => x.AggregateId == invoiceId)
                .ToListAsync());
        }
        finally
        {
            await CleanupPaymentTestDataAsync(
                db, customerId, invoiceId, operationId);
        }
    }

    [Theory]
    [InlineData(
        PaymentOutcome.Approved,
        InvoiceStatus.Paid,
        "InvoicePaid.v1")]
    [InlineData(
        PaymentOutcome.Rejected,
        InvoiceStatus.PaymentFailed,
        "InvoicePaymentFailed.v1")]
    [InlineData(
        PaymentOutcome.Unknown,
        InvoiceStatus.PaymentUnknown,
        "InvoicePaymentUnknown.v1")]
    public async Task CompletePaymentAsync_ShouldPersistOutcomeAndOutboxEvent_AndBeIdempotent(
        PaymentOutcome outcome,
        InvoiceStatus expectedStatus,
        string expectedMessageType)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<VehicleBillingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        await using var db = new VehicleBillingDbContext(options);

        try
        {
            await CreatePaymentTestDataAsync(
                db, customerId, invoiceId, operationId);

            var repository = new InvoiceRepository(
                db,
                TimeProvider.System);

            var started = await repository.StartPaymentAsync(invoiceId);

            Assert.NotNull(started);
            Assert.Equal("PaymentProcessing", started.Status);

            var result = await repository.CompletePaymentAsync(
                invoiceId,
                outcome);

            Assert.Equal(expectedStatus.ToString(), result.Status);

            var persistedInvoice = await db.Invoices
                .AsNoTracking()
                .SingleAsync(x => x.Id == invoiceId);

            Assert.Equal(expectedStatus, persistedInvoice.Status);

            var messages = await db.OutboxMessages
                .AsNoTracking()
                .Where(x => x.AggregateId == invoiceId)
                .ToListAsync();

            var message = Assert.Single(messages);

            Assert.Equal(expectedMessageType, message.MessageType);
            Assert.False(string.IsNullOrWhiteSpace(message.Payload));

            // Repetir o mesmo resultado não pode criar outro evento.
            var repeatedResult = await repository.CompletePaymentAsync(
                invoiceId,
                outcome);

            Assert.Equal(expectedStatus.ToString(), repeatedResult.Status);

            var finalMessageCount = await db.OutboxMessages
                .AsNoTracking()
                .CountAsync(x => x.AggregateId == invoiceId);

            Assert.Equal(1, finalMessageCount);
        }
        finally
        {
            await CleanupPaymentTestDataAsync(
                db, customerId, invoiceId, operationId);
        }
    }

    private static async Task CreatePaymentTestDataAsync(
        VehicleBillingDbContext db,
        Guid customerId,
        Guid invoiceId,
        Guid operationId)
    {
        var customer = new Customer(
            customerId,
            $"Payment Integration Test {customerId:N}",
            CustomerPlan.Postpaid,
            Money.Zero,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

        db.Customers.Add(customer);

        var operation = new Operation(
            operationId,
            Guid.NewGuid(),
            customerId,
            OperationType.VehicleConsultation,
            Money.Of(12.50m),
            Guid.NewGuid().ToString("N"),
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        operation.MarkAwaitingInvoice();

        db.Operations.Add(operation);

        var invoice = new Invoice(
            invoiceId,
            customerId,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 11, 1),
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        invoice.AddItem(
            new InvoiceItem(
                invoiceId,
                operationId,
                Money.Of(12.50m)));

        invoice.FinalizeInvoice();

        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();
    }

    private static async Task CleanupPaymentTestDataAsync(
        VehicleBillingDbContext db,
        Guid customerId,
        Guid invoiceId,
        Guid operationId)
    {
        await db.OutboxMessages
            .Where(x => x.AggregateId == invoiceId)
            .ExecuteDeleteAsync();

        await db.InvoiceItems
            .Where(x => x.InvoiceId == invoiceId)
            .ExecuteDeleteAsync();

        await db.Invoices
            .Where(x => x.Id == invoiceId)
            .ExecuteDeleteAsync();

        await db.Operations
            .Where(x => x.Id == operationId)
            .ExecuteDeleteAsync();

        await db.Customers
            .Where(x => x.Id == customerId)
            .ExecuteDeleteAsync();
    }
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
            => _utcNow;
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


