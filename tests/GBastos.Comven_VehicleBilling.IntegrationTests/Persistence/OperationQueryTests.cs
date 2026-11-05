using Comven_VehicleBilling.Application.Operations;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Comven_VehicleBilling.IntegrationTests.Persistence;

public sealed class OperationQueryTests
{
  private static VehicleBillingDbContext CreateDbContext()
  {
    var connectionString =
        Environment.GetEnvironmentVariable("VEHICLE_BILLING_TEST_CONNECTION_STRING");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException(
          "A variável VEHICLE_BILLING_TEST_CONNECTION_STRING não foi definida.");
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
        $"Operation Query Test {customerId:N}",
        CustomerPlan.Prepaid,
        Money.Of(1000.00m),
        DateTimeOffset.UtcNow);

    await using var db = CreateDbContext();

    db.Customers.Add(customer);
    await db.SaveChangesAsync();

    return customerId;
  }

  private static CreateOperationCommand NewCommand(
      Guid customerId,
      OperationType operationType,
      decimal amount = 10.00m)
  {
    return new CreateOperationCommand(
        Guid.NewGuid(),
        customerId,
        operationType.ToString(),
        amount);
  }

  private static async Task<OperationDto> CreateOperationAsync(
      Guid customerId,
      OperationType operationType,
      decimal amount = 10.00m)
  {
    await using var db = CreateDbContext();

    var repository = new OperationRepository(db);

    return await repository.CreateAsync(
        NewCommand(customerId, operationType, amount));
  }

  private static async Task CleanupAsync(Guid customerId)
  {
    await using var db = CreateDbContext();

    var operations = await db.Operations
        .Where(x => x.CustomerId == customerId)
        .ToListAsync();

    var operationIds = operations
        .Select(x => x.Id)
        .ToArray();

    if (operationIds.Length > 0)
    {
      var outboxMessages = await db.OutboxMessages
          .Where(x =>
              x.AggregateId.HasValue &&
              operationIds.Contains(x.AggregateId.Value))
          .ToListAsync();

      var ledgerEntries = await db.LedgerEntries
          .Where(x =>
              x.OperationId.HasValue &&
              operationIds.Contains(x.OperationId.Value))
          .ToListAsync();

      db.OutboxMessages.RemoveRange(outboxMessages);
      db.LedgerEntries.RemoveRange(ledgerEntries);
      db.Operations.RemoveRange(operations);
    }

    var customer = await db.Customers
        .FirstOrDefaultAsync(x => x.Id == customerId);

    if (customer is not null)
    {
      db.Customers.Remove(customer);
    }

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetAllAsync_Must_filter_by_customer_and_operation_type()
  {
    var customerId = await CreateCustomerAsync();

    try
    {
      await CreateOperationAsync(
          customerId,
          OperationType.PlateConsultation);

      await CreateOperationAsync(
          customerId,
          OperationType.VehicleConsultation);

      await CreateOperationAsync(
          customerId,
          OperationType.PlateConsultation);

      await using var db = CreateDbContext();

      var repository = new OperationRepository(db);

      var result = await repository.GetAllAsync(
          customerId,
          OperationType.PlateConsultation.ToString(),
          null,
          null,
          1,
          50);

      Assert.Equal(2, result.TotalCount);

      Assert.All(
          result.Items,
          item => Assert.Equal(
              OperationType.PlateConsultation.ToString(),
              item.OperationType));
    }
    finally
    {
      await CleanupAsync(customerId);
    }
  }

  [Fact]
  public async Task GetAllAsync_Must_filter_by_status()
  {
    var customerId = await CreateCustomerAsync();

    try
    {
      await CreateOperationAsync(
          customerId,
          OperationType.PlateConsultation);

      await CreateOperationAsync(
          customerId,
          OperationType.VehicleConsultation);

      await using var db = CreateDbContext();

      var repository = new OperationRepository(db);

      var result = await repository.GetAllAsync(
          customerId,
          null,
          null,
          null,
          1,
          50,
          status: OperationStatus.Charged.ToString());

      Assert.Equal(2, result.TotalCount);

      Assert.All(
          result.Items,
          item => Assert.Equal(
              OperationStatus.Charged.ToString(),
              item.Status));
    }
    finally
    {
      await CleanupAsync(customerId);
    }
  }

  [Fact]
  public async Task GetAllAsync_Must_filter_by_date_range()
  {
    var customerId = await CreateCustomerAsync();

    try
    {
      var startDate = DateTimeOffset.UtcNow.AddMinutes(-1);

      await CreateOperationAsync(
          customerId,
          OperationType.PlateConsultation);

      await CreateOperationAsync(
          customerId,
          OperationType.VehicleConsultation);

      var endDate = DateTimeOffset.UtcNow.AddMinutes(1);

      await using var db = CreateDbContext();

      var repository = new OperationRepository(db);

      var result = await repository.GetAllAsync(
          customerId,
          null,
          startDate,
          endDate,
          1,
          50);

      Assert.Equal(2, result.TotalCount);

      Assert.All(
          result.Items,
          item =>
          {
            Assert.True(item.CreatedAt >= startDate);
            Assert.True(item.CreatedAt <= endDate);
          });
    }
    finally
    {
      await CleanupAsync(customerId);
    }
  }

  [Fact]
  public async Task GetAllAsync_Must_return_correct_pagination_metadata()
  {
    var customerId = await CreateCustomerAsync();

    try
    {
      for (var i = 0; i < 5; i++)
      {
        await CreateOperationAsync(
            customerId,
            OperationType.PlateConsultation);
      }

      await using var db = CreateDbContext();

      var repository = new OperationRepository(db);

      var firstPage = await repository.GetAllAsync(
          customerId,
          null,
          null,
          null,
          1,
          2);

      var secondPage = await repository.GetAllAsync(
          customerId,
          null,
          null,
          null,
          2,
          2);

      var lastPage = await repository.GetAllAsync(
          customerId,
          null,
          null,
          null,
          3,
          2);

      Assert.Equal(5, firstPage.TotalCount);
      Assert.Equal(3, firstPage.TotalPages);
      Assert.Equal(2, firstPage.Items.Count);
      Assert.True(firstPage.HasNextPage);
      Assert.False(firstPage.HasPreviousPage);

      Assert.Equal(2, secondPage.Page);
      Assert.Equal(2, secondPage.Items.Count);
      Assert.True(secondPage.HasNextPage);
      Assert.True(secondPage.HasPreviousPage);

      Assert.Equal(3, lastPage.Page);
      Assert.Single(lastPage.Items);
      Assert.False(lastPage.HasNextPage);
      Assert.True(lastPage.HasPreviousPage);
    }
    finally
    {
      await CleanupAsync(customerId);
    }
  }

  [Fact]
  public async Task GetAllAsync_Must_return_empty_page_when_no_operations_match()
  {
    var customerId = await CreateCustomerAsync();

    try
    {
      await CreateOperationAsync(
          customerId,
          OperationType.PlateConsultation);

      await using var db = CreateDbContext();

      var repository = new OperationRepository(db);

      var result = await repository.GetAllAsync(
          customerId,
          OperationType.CrlveIssuance.ToString(),
          null,
          null,
          1,
          50);

      Assert.Empty(result.Items);
      Assert.Equal(0, result.TotalCount);
      Assert.Equal(0, result.TotalPages);
      Assert.False(result.HasNextPage);
      Assert.False(result.HasPreviousPage);
    }
    finally
    {
      await CleanupAsync(customerId);
    }
  }

  [Fact]
  public async Task GetAllAsync_Must_return_operations_in_descending_creation_order()
  {
    var customerId = await CreateCustomerAsync();

    try
    {
      for (var i = 0; i < 5; i++)
      {
        await CreateOperationAsync(
            customerId,
            OperationType.PlateConsultation);
      }

      await using var db = CreateDbContext();

      var repository = new OperationRepository(db);

      var result = await repository.GetAllAsync(
          customerId,
          null,
          null,
          null,
          1,
          50);

      var expected = result.Items
          .OrderByDescending(x => x.CreatedAt)
          .ThenByDescending(x => x.Id)
          .ToArray();

      Assert.Equal(expected, result.Items);
    }
    finally
    {
      await CleanupAsync(customerId);
    }
  }
}
