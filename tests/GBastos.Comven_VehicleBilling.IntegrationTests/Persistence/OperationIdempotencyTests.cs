using Comven_VehicleBilling.Application.Operations;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

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
                "Defina VEHICLE_BILLING_TEST_CONNECTION_STRING " +
                "para um banco PostgreSQL exclusivo de testes.");
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
            CustomerPlan.Prepaid,
            Money.Of(120.01m),
            DateTimeOffset.UtcNow);

        await using var db = CreateDbContext();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return customerId;
    }

    private static CreateOperationCommand NewCommand(
        Guid externalId,
        Guid customerId,
        decimal amount = 70.01m)
    {
        return new CreateOperationCommand(
            externalId,
            customerId,
            OperationType.PlateConsultation.ToString(),
            amount);
    }

  private async Task CleanupAsync(Guid externalId, Guid customerId)
  {
    await using var db = CreateDbContext();

    // Localiza todas as operações do cliente associadas
    // ao identificador externo informado.
    var operations = await db.Operations
        .Where(operation =>
            operation.ExternalOperationId == externalId &&
            operation.CustomerId == customerId)
        .ToListAsync();

    var operationIds = operations
        .Select(operation => operation.Id)
        .ToList();

    var outboxMessages = await db.OutboxMessages
        .Where(message =>
            message.AggregateId.HasValue &&
            operationIds.Contains(message.AggregateId.Value))
        .ToListAsync();

    db.OutboxMessages.RemoveRange(outboxMessages);

    var ledgerEntries = await db.LedgerEntries
        .Where(entry =>
            entry.OperationId.HasValue &&
            operationIds.Contains(entry.OperationId.Value))
        .ToListAsync();

    db.LedgerEntries.RemoveRange(ledgerEntries);
    db.Operations.RemoveRange(operations);

    await db.SaveChangesAsync();

    // Só remove o cliente quando não houver outras operações
    // vinculadas a ele.
    var customer = await db.Customers
        .SingleOrDefaultAsync(item => item.Id == customerId);

    if (customer is not null)
    {
      var hasOperations = await db.Operations
          .AnyAsync(operation => operation.CustomerId == customerId);

      if (!hasOperations)
      {
        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
      }
    }
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

            Assert.Equal(
                1,
                await verifyDb.Operations.CountAsync(
                    x => x.ExternalOperationId == externalId));
        }
        finally
        {
            await CleanupAsync(externalId, customerId);
        }
    }

    [Fact]
    public async Task Reusing_external_id_with_different_amount_must_be_rejected()
    {
        var customerId = await CreateCustomerAsync();
        var externalId = Guid.NewGuid();

        try
        {
            await using (var db = CreateDbContext())
            {
                await new OperationRepository(db).CreateAsync(
                    NewCommand(externalId, customerId, 70.01m));
            }

            await using (var db = CreateDbContext())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => new OperationRepository(db).CreateAsync(
                        NewCommand(externalId, customerId, 60.01m)));
            }

            await using var verifyDb = CreateDbContext();

            var operations = await verifyDb.Operations
                .Where(x => x.ExternalOperationId == externalId)
                .ToListAsync();

            Assert.Single(operations);
            Assert.Equal(70.01m, operations[0].Amount.Amount);
        }
        finally
        {
            await CleanupAsync(externalId, customerId);
        }
    }

    [Fact]
    public async Task Twenty_concurrent_requests_must_charge_only_once()
    {
        const int requestCount = 20;
        const decimal operationAmount = 70.01m;
        const decimal expectedBalance = 50.00m;

        var customerId = await CreateCustomerAsync();
        var externalId = Guid.NewGuid();

        try
        {
            var command = NewCommand(
                externalId,
                customerId,
                operationAmount);

            var start = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, requestCount)
                .Select(async _ =>
                {
                    await start.Task;

                    await using var db = CreateDbContext();

                    return await new OperationRepository(db)
                        .CreateAsync(command);
                })
                .ToArray();

            start.SetResult(true);

            var results = await Task.WhenAll(tasks);

            Assert.Equal(requestCount, results.Length);

            var operationId = results[0].Id;

            Assert.All(
                results,
                result => Assert.Equal(operationId, result.Id));

            await using var verifyDb = CreateDbContext();

            var operations = await verifyDb.Operations
                .Where(x => x.ExternalOperationId == externalId)
                .ToListAsync();

            Assert.Single(operations);
            Assert.Equal(operationAmount, operations[0].Amount.Amount);

            var ledgerEntries = await verifyDb.LedgerEntries
                .Where(x => x.OperationId == operationId)
                .ToListAsync();

            Assert.Single(ledgerEntries);
            Assert.Equal(operationAmount, ledgerEntries[0].Amount.Amount);
            Assert.Equal(LedgerEntryType.Debit, ledgerEntries[0].Type);

            var customer = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(x => x.Id == customerId);

            Assert.Equal(expectedBalance, customer.Balance.Amount);
        }
        finally
        {
            await CleanupAsync(externalId, customerId);
        }
    }

  [Fact]
  public async Task Concurrent_different_operations_must_not_overdraw_balance()
  {
    const decimal operationAmount = 70.01m;
    const decimal expectedBalance = 50.00m;

    var customerId = await CreateCustomerAsync();
    var externalIdA = Guid.NewGuid();
    var externalIdB = Guid.NewGuid();

    try
    {
      var start = new TaskCompletionSource<bool>(
          TaskCreationOptions.RunContinuationsAsynchronously);

      async Task<bool> TryCreateAsync(Guid externalId)
      {
        await start.Task;

        await using var db = CreateDbContext();

        try
        {
          await new OperationRepository(db).CreateAsync(
              NewCommand(externalId, customerId, operationAmount));

          return true;
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "Saldo insuficiente",
                StringComparison.OrdinalIgnoreCase))
        {
          return false;
        }
      }

      var taskA = TryCreateAsync(externalIdA);
      var taskB = TryCreateAsync(externalIdB);

      start.SetResult(true);

      var results = await Task.WhenAll(taskA, taskB);

      Assert.Equal(1, results.Count(accepted => accepted));
      Assert.Equal(1, results.Count(accepted => !accepted));

      await using var verifyDb = CreateDbContext();

      var operations = await verifyDb.Operations
          .Where(x =>
              x.ExternalOperationId == externalIdA ||
              x.ExternalOperationId == externalIdB)
          .ToListAsync();

      var operation = Assert.Single(operations);

      Assert.Equal(operationAmount, operation.Amount.Amount);

      var ledgerEntries = await verifyDb.LedgerEntries
          .Where(x => x.OperationId == operation.Id)
          .ToListAsync();

      var ledgerEntry = Assert.Single(ledgerEntries);

      Assert.Equal(operationAmount, ledgerEntry.Amount.Amount);
      Assert.Equal(LedgerEntryType.Debit, ledgerEntry.Type);

      var customer = await verifyDb.Customers
          .AsNoTracking()
          .SingleAsync(x => x.Id == customerId);

      Assert.Equal(expectedBalance, customer.Balance.Amount);
      Assert.True(customer.Balance.Amount >= 0);
    }
    finally
    {
      await CleanupAsync(externalIdA, customerId);
      await CleanupAsync(externalIdB, customerId);
    }
  }

  [Fact]
  public async Task Concurrent_same_external_id_for_different_customers_must_be_rejected()
  {
    const decimal operationAmount = 70.01m;

    var customerA = await CreateCustomerAsync();
    var customerB = await CreateCustomerAsync();
    var externalId = Guid.NewGuid();

    try
    {
      var start = new TaskCompletionSource<bool>(
          TaskCreationOptions.RunContinuationsAsynchronously);

      async Task<OperationDto?> TryCreateAsync(Guid customerId)
      {
        await start.Task;

        await using var db = CreateDbContext();

        try
        {
          return await new OperationRepository(db).CreateAsync(
              NewCommand(externalId, customerId, operationAmount));
        }
        catch (InvalidOperationException)
        {
          return null;
        }
      }

      var taskA = TryCreateAsync(customerA);
      var taskB = TryCreateAsync(customerB);

      start.SetResult(true);

      var results = await Task.WhenAll(taskA, taskB);

      var successfulOperation = Assert.Single(
          results.Where(result => result is not null));

      Assert.Equal(externalId, successfulOperation.ExternalOperationId);

      await using var verifyDb = CreateDbContext();

      var operations = await verifyDb.Operations
          .Where(x => x.ExternalOperationId == externalId)
          .ToListAsync();

      var operation = Assert.Single(operations);

      Assert.Equal(successfulOperation.Id, operation.Id);

      var ledgerEntries = await verifyDb.LedgerEntries
          .Where(x => x.OperationId == operation.Id)
          .ToListAsync();

      Assert.Single(ledgerEntries);

      var winningCustomer = await verifyDb.Customers
          .AsNoTracking()
          .SingleAsync(x => x.Id == operation.CustomerId);

      var losingCustomerId = operation.CustomerId == customerA
          ? customerB
          : customerA;

      var losingCustomer = await verifyDb.Customers
          .AsNoTracking()
          .SingleAsync(x => x.Id == losingCustomerId);

      Assert.Equal(50.00m, winningCustomer.Balance.Amount);
      Assert.Equal(120.01m, losingCustomer.Balance.Amount);
    }
    finally
    {
      await CleanupAsync(externalId, customerA);
      await CleanupAsync(externalId, customerB);
    }
  }

}
