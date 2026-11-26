using Comven_VehicleBilling.Application.Abstractions.Payments;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.PaymentsProvider;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.IntegrationTests.Persistence;

[Collection("PostgreSQL integration tests")]
public sealed class PaymentProviderTests
{
    [Fact]
    public async Task ProcessPaymentAsync_ShouldPersistApprovedPayment_AndAllowLookup()
    {
        await WithScenarioAsync(async scenario =>
        {
            var provider = CreateProvider(scenario);

            var request = CreateRequest(scenario);

            var result = await provider.ProcessPaymentAsync(request);

            Assert.Equal(PaymentOutcome.Approved, result.Outcome);
            Assert.Equal(
                $"PAY-{scenario.InvoiceId:N}",
                result.ExternalPaymentId);
            Assert.Null(result.ErrorMessage);

            var recovered = await provider.GetPaymentAsync(
                scenario.InvoiceId.ToString("N"));

            Assert.NotNull(recovered);
            Assert.Equal(result, recovered);

            await using var db = CreateDbContext(scenario.Options);

            var persisted = await db.PaymentProviderTransactions
                .AsNoTracking()
                .SingleAsync(x => x.InvoiceId == scenario.InvoiceId);

            Assert.Equal(request.CustomerId, persisted.CustomerId);
            Assert.Equal(request.Amount, persisted.Amount);
            Assert.Equal(
                scenario.InvoiceId.ToString("N"),
                persisted.IdempotencyKey);
        });
    }

  [Fact]
  public async Task GetPaymentAsync_ShouldRecoverPayment_AfterProviderRecreation()
  {
    await WithScenarioAsync(async scenario =>
    {
      var request = CreateRequest(scenario);

      PaymentResult original;

      // Primeira instÃƒÂ¢ncia: processa e persiste o pagamento.
      var firstProvider = CreateProvider(scenario);

      original = await firstProvider.ProcessPaymentAsync(request);

      Assert.Equal(PaymentOutcome.Approved, original.Outcome);

      // Segunda instÃƒÂ¢ncia: simula a recuperaÃƒÂ§ÃƒÂ£o apÃƒÂ³s reinicializaÃƒÂ§ÃƒÂ£o.
      var restartedProvider = CreateProvider(scenario);

      var recovered = await restartedProvider.GetPaymentAsync(
          scenario.InvoiceId.ToString("N"));

      Assert.NotNull(recovered);

      Assert.Equal(original, recovered);

      Assert.Equal(
          $"PAY-{scenario.InvoiceId:N}",
          recovered.ExternalPaymentId);

      Assert.Equal(PaymentOutcome.Approved, recovered.Outcome);
      Assert.Null(recovered.ErrorMessage);
    });
  }

  [Fact]
    public async Task ProcessPaymentAsync_ShouldBeIdempotent_WhenSameRequestIsRepeated()
    {
        await WithScenarioAsync(async scenario =>
        {
            var provider = CreateProvider(scenario);
            var request = CreateRequest(scenario);

            var first = await provider.ProcessPaymentAsync(request);
            var second = await provider.ProcessPaymentAsync(request);

            Assert.Equal(first, second);

            await using var db = CreateDbContext(scenario.Options);

            var count = await db.PaymentProviderTransactions
                .CountAsync(x => x.InvoiceId == scenario.InvoiceId);

            Assert.Equal(1, count);
        });
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldRejectDifferentAmount_WhenKeyAlreadyExists()
    {
        await WithScenarioAsync(async scenario =>
        {
            var provider = CreateProvider(scenario);
            var request = CreateRequest(scenario);

            await provider.ProcessPaymentAsync(request);

            var conflictingRequest = request with
            {
                Amount = request.Amount + 10m
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.ProcessPaymentAsync(conflictingRequest));

            Assert.Equal(
                "The idempotency key was already used with different payment data.",
                exception.Message);

            await using var db = CreateDbContext(scenario.Options);

            var count = await db.PaymentProviderTransactions
                .CountAsync(x => x.InvoiceId == scenario.InvoiceId);

            Assert.Equal(1, count);
        });
    }

  [Fact]
  public async Task ProcessPaymentAsync_ShouldRejectDifferentCustomer_WhenKeyAlreadyExists()
  {
    await WithScenarioAsync(async scenario =>
    {
      var provider = CreateProvider(scenario);
      var request = CreateRequest(scenario);

      await provider.ProcessPaymentAsync(request);

      var conflictingRequest = request with
      {
        CustomerId = Guid.NewGuid()
      };

      var exception = await Assert.ThrowsAsync<InvalidOperationException>(
          () => provider.ProcessPaymentAsync(conflictingRequest));

      Assert.Equal(
          "The idempotency key was already used with different payment data.",
          exception.Message);

      await using var db = CreateDbContext(scenario.Options);

      var persisted = await db.PaymentProviderTransactions
          .AsNoTracking()
          .SingleAsync(x => x.InvoiceId == scenario.InvoiceId);

      Assert.Equal(scenario.CustomerId, persisted.CustomerId);
      Assert.Equal(request.Amount, persisted.Amount);
    });
  }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldPersistOnlyOnePayment_WhenRequestsAreConcurrent()
    {
        await WithScenarioAsync(async scenario =>
        {
            var provider = CreateProvider(scenario);
            var request = CreateRequest(scenario);

            var tasks = Enumerable.Range(0, 8)
                .Select(_ => provider.ProcessPaymentAsync(request));

            var results = await Task.WhenAll(tasks);

            Assert.All(
                results,
                result => Assert.Equal(
                    PaymentOutcome.Approved,
                    result.Outcome));

            Assert.All(
                results,
                result => Assert.Equal(results[0], result));

            await using var db = CreateDbContext(scenario.Options);

            var persisted = await db.PaymentProviderTransactions
                .AsNoTracking()
                .Where(x => x.InvoiceId == scenario.InvoiceId)
                .ToListAsync();

            Assert.Single(persisted);
        });
    }


    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotDuplicatePayment_AfterProviderRecreation()
    {
        await WithScenarioAsync(async scenario =>
        {
            var request = CreateRequest(scenario);

            var firstProvider = CreateProvider(scenario);
            var original = await firstProvider.ProcessPaymentAsync(request);

            var restartedProvider = CreateProvider(scenario);

            var recovered = await restartedProvider.GetPaymentAsync(
                request.IdempotencyKey);

            Assert.Equal(original, recovered);

            var retry = await restartedProvider.ProcessPaymentAsync(request);

            Assert.Equal(original, retry);

            await using var db = CreateDbContext(scenario.Options);

            var count = await db.PaymentProviderTransactions
                .CountAsync(x => x.InvoiceId == scenario.InvoiceId);

            Assert.Equal(1, count);
        });
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldPersistOnlyOnePayment_WhenIndependentProvidersAreConcurrent()
    {
        await WithScenarioAsync(async scenario =>
        {
            var request = CreateRequest(scenario);

            var tasks = Enumerable.Range(0, 8)
                .Select(_ =>
                {
                    var provider = CreateProvider(scenario);
                    return provider.ProcessPaymentAsync(request);
                });

            var results = await Task.WhenAll(tasks);

            Assert.All(results, result =>
                Assert.Equal(PaymentOutcome.Approved, result.Outcome));

            Assert.All(results, result =>
                Assert.Equal(results[0], result));

            await using var db = CreateDbContext(scenario.Options);

            var persisted = await db.PaymentProviderTransactions
                .AsNoTracking()
                .Where(x => x.InvoiceId == scenario.InvoiceId)
                .ToListAsync();

            Assert.Single(persisted);
        });
    }

  [Fact]
  public async Task GetPaymentAsync_ShouldRecoverUnknownPayment_AfterProviderRecreation()
  {
    await WithScenarioAsync(async scenario =>
    {
      var request = CreateRequest(scenario);

      var unknownTransaction = new PaymentProviderTransaction
      {
        Id = Guid.NewGuid(),
        IdempotencyKey = request.IdempotencyKey,
        InvoiceId = request.InvoiceId,
        CustomerId = request.CustomerId,
        Amount = request.Amount,
        Outcome = PaymentOutcome.Unknown,
        ExternalPaymentId = null,
        ErrorMessage = "Provider response timeout",
        CreatedAt = DateTime.UtcNow
      };

      await using (var db = CreateDbContext(scenario.Options))
      {
        db.PaymentProviderTransactions.Add(unknownTransaction);
        await db.SaveChangesAsync();
      }

      var provider = CreateProvider(scenario);

      var recovered = await provider.GetPaymentAsync(
          request.IdempotencyKey);

      Assert.NotNull(recovered);
      Assert.Equal(PaymentOutcome.Unknown, recovered.Outcome);
      Assert.Null(recovered.ExternalPaymentId);
      Assert.Equal(
          "Provider response timeout",
          recovered.ErrorMessage);

      await using var verificationDb =
          CreateDbContext(scenario.Options);

      var persisted = await verificationDb.PaymentProviderTransactions
          .AsNoTracking()
          .Where(x => x.InvoiceId == scenario.InvoiceId)
          .ToListAsync();

      Assert.Single(persisted);
      Assert.Equal(
          PaymentOutcome.Unknown,
          persisted[0].Outcome);
    });
  }

  [Fact]
    public async Task GetPaymentAsync_ShouldReturnNull_WhenPaymentDoesNotExist()
    {
        await WithScenarioAsync(async scenario =>
        {
            var provider = CreateProvider(scenario);

            var result = await provider.GetPaymentAsync(
                Guid.NewGuid().ToString("N"));

            Assert.Null(result);

            await using var db = CreateDbContext(scenario.Options);

            var count = await db.PaymentProviderTransactions
                .CountAsync(x => x.InvoiceId == scenario.InvoiceId);

            Assert.Equal(0, count);
        });
    }
    private static PaymentProvider CreateProvider(TestScenario scenario)
    {
        return new PaymentProvider(
            new TestDbContextFactory(scenario.Options));
    }

    private static PaymentRequest CreateRequest(TestScenario scenario)
    {
        return new PaymentRequest(
            scenario.InvoiceId,
            scenario.CustomerId,
            12.50m,
            scenario.InvoiceId.ToString("N"));
    }

    private static async Task WithScenarioAsync(
        Func<TestScenario, Task> test)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure VEHICLE_BILLING_TEST_CONNECTION_STRING.");

        var connection = new Npgsql.NpgsqlConnectionStringBuilder(
            connectionString);

        Assert.Equal("vehicle_billing_test", connection.Database);

        var options = new DbContextOptionsBuilder<VehicleBillingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var scenario = new TestScenario(
            options,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        try
        {
            await CreateInvoiceTestDataAsync(scenario);

            await test(scenario);
        }
        finally
        {
            await CleanupAsync(scenario);
        }
    }

    private static async Task CreateInvoiceTestDataAsync(
        TestScenario scenario)
    {
        await using var db = CreateDbContext(scenario.Options);

        var customer = new Customer(
            scenario.CustomerId,
            $"Payment Provider Test {scenario.CustomerId:N}",
            CustomerPlan.Postpaid,
            Money.Zero,
            DateTimeOffset.UtcNow);

        db.Customers.Add(customer);

        var operation = new Operation(
            scenario.OperationId,
            Guid.NewGuid(),
            scenario.CustomerId,
            OperationType.VehicleConsultation,
            Money.Of(12.50m),
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow);

        operation.MarkAwaitingInvoice();

        db.Operations.Add(operation);

        var invoice = new Invoice(
            scenario.InvoiceId,
            scenario.CustomerId,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 11, 1),
            DateTimeOffset.UtcNow);

        invoice.AddItem(
            new InvoiceItem(
                scenario.InvoiceId,
                scenario.OperationId,
                Money.Of(12.50m)));

        invoice.FinalizeInvoice();

        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();
    }

    private static async Task CleanupAsync(TestScenario scenario)
    {
        await using var db = CreateDbContext(scenario.Options);

        await db.PaymentProviderTransactions
            .Where(x => x.InvoiceId == scenario.InvoiceId)
            .ExecuteDeleteAsync();

        await db.InvoiceItems
            .Where(x => x.InvoiceId == scenario.InvoiceId)
            .ExecuteDeleteAsync();

        await db.Invoices
            .Where(x => x.Id == scenario.InvoiceId)
            .ExecuteDeleteAsync();

        await db.Operations
            .Where(x => x.Id == scenario.OperationId)
            .ExecuteDeleteAsync();

        await db.Customers
            .Where(x => x.Id == scenario.CustomerId)
            .ExecuteDeleteAsync();
    }

    private static VehicleBillingDbContext CreateDbContext(
        DbContextOptions<VehicleBillingDbContext> options)
    {
        return new VehicleBillingDbContext(options);
    }

    private sealed record TestScenario(
        DbContextOptions<VehicleBillingDbContext> Options,
        Guid CustomerId,
        Guid InvoiceId,
        Guid OperationId);

    private sealed class TestDbContextFactory
        : IDbContextFactory<VehicleBillingDbContext>
    {
        private readonly DbContextOptions<VehicleBillingDbContext> _options;

        public TestDbContextFactory(
            DbContextOptions<VehicleBillingDbContext> options)
        {
            _options = options;
        }

        public VehicleBillingDbContext CreateDbContext()
        {
            return new VehicleBillingDbContext(_options);
        }
    }
}




