using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Infrastructure.Persistence.Repositories;
using Comven_VehicleBilling.Application.BalanceCredits;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.IntegrationTests.Persistence;

public sealed class BalanceCreditIdempotencyTests
{
    private const decimal InitialBalance = 120.01m;

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
            $"Balance Credit Test {customerId:N}",
            CustomerPlan.Prepaid,
            Money.Of(InitialBalance),
            DateTimeOffset.UtcNow);

        await using var db = CreateDbContext();

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return customerId;
    }

    private static async Task CleanupAsync(
        IEnumerable<Guid> customerIds,
        IEnumerable<string> externalReferences)
    {
        var ids = customerIds.Distinct().ToArray();
        var references = externalReferences.Distinct().ToArray();

        await using var db = CreateDbContext();

        var credits = await db.BalanceCredits
            .Where(credit =>
                ids.Contains(credit.CustomerId) ||
                references.Contains(credit.ExternalReference))
            .ToListAsync();

        var creditIds = credits
            .Select(credit => credit.Id)
            .ToArray();

        var ledgerEntries = await db.LedgerEntries
            .Where(entry =>
                entry.BalanceCreditId.HasValue &&
                creditIds.Contains(entry.BalanceCreditId.Value))
            .ToListAsync();

        db.LedgerEntries.RemoveRange(ledgerEntries);
        db.BalanceCredits.RemoveRange(credits);

        var customers = await db.Customers
            .Where(customer => ids.Contains(customer.Id))
            .ToListAsync();

        db.Customers.RemoveRange(customers);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task First_credit_must_update_balance_and_create_one_ledger_entry()
    {
        var customerId = await CreateCustomerAsync();
        var externalReference = $"credit-{Guid.NewGuid():N}";
        const decimal amount = 25.50m;

        try
        {
            BalanceCreditResult result;

            await using (var db = CreateDbContext())
            {
                result = await new BalanceCreditRepository(db).CreditAsync(
                    customerId,
                    externalReference,
                    amount);
            }

            Assert.False(result.AlreadyProcessed);
            Assert.Equal(customerId, result.CustomerId);
            Assert.Equal(amount, result.Amount);
            Assert.Equal(InitialBalance + amount, result.Balance);

            await using var verifyDb = CreateDbContext();

            var credit = await verifyDb.BalanceCredits
                .SingleAsync(item =>
                    item.ExternalReference == externalReference);

            Assert.Equal(customerId, credit.CustomerId);
            Assert.Equal(amount, credit.Amount.Amount);

            var ledgerEntry = await verifyDb.LedgerEntries
                .SingleAsync(entry =>
                    entry.BalanceCreditId == credit.Id);

            Assert.Equal(customerId, ledgerEntry.CustomerId);
            Assert.Equal(amount, ledgerEntry.Amount.Amount);
            Assert.Equal(LedgerEntryType.Credit, ledgerEntry.Type);
            Assert.Null(ledgerEntry.OperationId);
            Assert.Equal(credit.Id, ledgerEntry.BalanceCreditId);

            var customer = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Id == customerId);

            Assert.Equal(InitialBalance + amount, customer.Balance.Amount);
        }
        finally
        {
            await CleanupAsync(
                new[] { customerId },
                new[] { externalReference });
        }
    }

    [Fact]
    public async Task Repeated_credit_with_same_reference_must_not_duplicate_balance()
    {
        var customerId = await CreateCustomerAsync();
        var externalReference = $"credit-{Guid.NewGuid():N}";
        const decimal amount = 30.00m;

        try
        {
            BalanceCreditResult first;

            await using (var db = CreateDbContext())
            {
                first = await new BalanceCreditRepository(db).CreditAsync(
                    customerId,
                    externalReference,
                    amount);
            }

            BalanceCreditResult second;

            await using (var db = CreateDbContext())
            {
                second = await new BalanceCreditRepository(db).CreditAsync(
                    customerId,
                    externalReference,
                    amount);
            }

            Assert.False(first.AlreadyProcessed);
            Assert.True(second.AlreadyProcessed);
            Assert.Equal(first.CreditId, second.CreditId);
            Assert.Equal(first.Amount, second.Amount);
            Assert.Equal(
                InitialBalance + amount,
                second.Balance);

            await using var verifyDb = CreateDbContext();

            Assert.Equal(
                1,
                await verifyDb.BalanceCredits.CountAsync(
                    item => item.ExternalReference == externalReference));

            Assert.Equal(
                1,
                await verifyDb.LedgerEntries.CountAsync(
                    entry => entry.BalanceCreditId == first.CreditId));

            var customer = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Id == customerId);

            Assert.Equal(
                InitialBalance + amount,
                customer.Balance.Amount);
        }
        finally
        {
            await CleanupAsync(
                new[] { customerId },
                new[] { externalReference });
        }
    }

    [Fact]
    public async Task Reusing_external_reference_with_different_amount_must_be_rejected()
    {
        var customerId = await CreateCustomerAsync();
        var externalReference = $"credit-{Guid.NewGuid():N}";

        try
        {
            await using (var db = CreateDbContext())
            {
                await new BalanceCreditRepository(db).CreditAsync(
                    customerId,
                    externalReference,
                    40.00m);
            }

            await using (var db = CreateDbContext())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => new BalanceCreditRepository(db).CreditAsync(
                        customerId,
                        externalReference,
                        50.00m));
            }

            await using var verifyDb = CreateDbContext();

            var credit = await verifyDb.BalanceCredits
                .SingleAsync(item =>
                    item.ExternalReference == externalReference);

            Assert.Equal(40.00m, credit.Amount.Amount);

            var customer = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Id == customerId);

            Assert.Equal(
                InitialBalance + 40.00m,
                customer.Balance.Amount);

            Assert.Equal(
                1,
                await verifyDb.LedgerEntries.CountAsync(
                    entry => entry.BalanceCreditId == credit.Id));
        }
        finally
        {
            await CleanupAsync(
                new[] { customerId },
                new[] { externalReference });
        }
    }

    [Fact]
    public async Task Same_external_reference_for_different_customers_must_be_rejected()
    {
        var customerA = await CreateCustomerAsync();
        var customerB = await CreateCustomerAsync();
        var externalReference = $"credit-{Guid.NewGuid():N}";

        try
        {
            BalanceCreditResult first;

            await using (var db = CreateDbContext())
            {
                first = await new BalanceCreditRepository(db).CreditAsync(
                    customerA,
                    externalReference,
                    35.00m);
            }

            await using (var db = CreateDbContext())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => new BalanceCreditRepository(db).CreditAsync(
                        customerB,
                        externalReference,
                        35.00m));
            }

            await using var verifyDb = CreateDbContext();

            var credit = await verifyDb.BalanceCredits
                .SingleAsync(item =>
                    item.ExternalReference == externalReference);

            Assert.Equal(customerA, credit.CustomerId);
            Assert.Equal(first.CreditId, credit.Id);

            var customerAAfter = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Id == customerA);

            var customerBAfter = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Id == customerB);

            Assert.Equal(
                InitialBalance + 35.00m,
                customerAAfter.Balance.Amount);

            Assert.Equal(
                InitialBalance,
                customerBAfter.Balance.Amount);

            Assert.Equal(
                1,
                await verifyDb.LedgerEntries.CountAsync(
                    entry => entry.BalanceCreditId == credit.Id));
        }
        finally
        {
            await CleanupAsync(
                new[] { customerA, customerB },
                new[] { externalReference });
        }
    }

    [Fact]
    public async Task Twenty_concurrent_requests_with_same_reference_must_create_one_credit()
    {
        const int requestCount = 20;
        const decimal amount = 15.25m;

        var customerId = await CreateCustomerAsync();
        var externalReference = $"credit-{Guid.NewGuid():N}";

        try
        {
            var start = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, requestCount)
                .Select(async _ =>
                {
                    await start.Task;

                    await using var db = CreateDbContext();

                    return await new BalanceCreditRepository(db).CreditAsync(
                        customerId,
                        externalReference,
                        amount);
                })
                .ToArray();

            start.SetResult(true);

            var results = await Task.WhenAll(tasks);

            Assert.Equal(requestCount, results.Length);

            var creditId = results[0].CreditId;

            Assert.All(
                results,
                result => Assert.Equal(creditId, result.CreditId));

            Assert.Single(
                results,
                result => !result.AlreadyProcessed);

            Assert.Equal(
                requestCount - 1,
                results.Count(result => result.AlreadyProcessed));

            await using var verifyDb = CreateDbContext();

            Assert.Equal(
                1,
                await verifyDb.BalanceCredits.CountAsync(
                    item => item.ExternalReference == externalReference));

            Assert.Equal(
                1,
                await verifyDb.LedgerEntries.CountAsync(
                    entry => entry.BalanceCreditId == creditId));

            var customer = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Id == customerId);

            Assert.Equal(
                InitialBalance + amount,
                customer.Balance.Amount);
        }
        finally
        {
            await CleanupAsync(
                new[] { customerId },
                new[] { externalReference });
        }
    }

    [Fact]
    public async Task Concurrent_credits_with_different_references_must_preserve_all_balance_updates()
    {
        const int requestCount = 20;
        const decimal amount = 5.00m;

        var customerId = await CreateCustomerAsync();

        var externalReferences = Enumerable.Range(0, requestCount)
            .Select(_ => $"credit-{Guid.NewGuid():N}")
            .ToArray();

        try
        {
            var start = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = externalReferences
                .Select(async externalReference =>
                {
                    await start.Task;

                    await using var db = CreateDbContext();

                    return await new BalanceCreditRepository(db).CreditAsync(
                        customerId,
                        externalReference,
                        amount);
                })
                .ToArray();

            start.SetResult(true);

            var results = await Task.WhenAll(tasks);

            Assert.Equal(requestCount, results.Length);
            Assert.All(results, result =>
                Assert.False(result.AlreadyProcessed));

            await using var verifyDb = CreateDbContext();

            var credits = await verifyDb.BalanceCredits
                .Where(item =>
                    externalReferences.Contains(item.ExternalReference))
                .ToListAsync();

            Assert.Equal(requestCount, credits.Count);

            var creditIds = credits
                .Select(item => item.Id)
                .ToArray();

            var ledgerEntries = await verifyDb.LedgerEntries
                .Where(entry =>
                    entry.BalanceCreditId.HasValue &&
                    creditIds.Contains(entry.BalanceCreditId.Value))
                .ToListAsync();

            Assert.Equal(requestCount, ledgerEntries.Count);

            Assert.All(
                ledgerEntries,
                entry =>
                {
                    Assert.Equal(customerId, entry.CustomerId);
                    Assert.Equal(amount, entry.Amount.Amount);
                    Assert.Equal(LedgerEntryType.Credit, entry.Type);
                    Assert.Null(entry.OperationId);
                });

            var customer = await verifyDb.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Id == customerId);

            Assert.Equal(
                InitialBalance + (requestCount * amount),
                customer.Balance.Amount);
        }
        finally
        {
            await CleanupAsync(
                new[] { customerId },
                externalReferences);
        }
    }
}


