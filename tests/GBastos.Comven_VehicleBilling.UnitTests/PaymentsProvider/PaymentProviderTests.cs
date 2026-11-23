using Comven_VehicleBilling.Application.Abstractions.Payments;
using Comven_VehicleBilling.Infrastructure.PaymentsProvider;

namespace Comven_VehicleBilling.UnitTests.PaymentsProvider;

public sealed class PaymentProviderTests
{
    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnSamePaymentForRepeatedIdempotencyKey()
    {
        var provider = new PaymentProvider();
        var request = CreateRequest();

        var first = await provider.ProcessPaymentAsync(request);
        var second = await provider.ProcessPaymentAsync(request);

        Assert.Equal(first, second);
        Assert.Equal(PaymentOutcome.Approved, first.Outcome);
        Assert.Equal($"PAY-{request.IdempotencyKey}", first.ExternalPaymentId);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnSinglePaymentForConcurrentRequests()
    {
        var provider = new PaymentProvider();
        var request = CreateRequest();

        const int concurrentRequests = 100;

        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(
                () => provider.ProcessPaymentAsync(request)));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(concurrentRequests, results.Length);

        var externalPaymentIds = results
            .Select(result => result.ExternalPaymentId)
            .Distinct()
            .ToArray();

        Assert.Single(externalPaymentIds);
        Assert.Equal(
            $"PAY-{request.IdempotencyKey}",
            externalPaymentIds[0]);

        Assert.All(
            results,
            result => Assert.Equal(PaymentOutcome.Approved, result.Outcome));

        var persisted = await provider.GetPaymentAsync(
            request.IdempotencyKey);

        Assert.NotNull(persisted);
        Assert.Equal(results[0], persisted);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldRejectDifferentCustomerForSameKey()
    {
        var provider = new PaymentProvider();
        var request = CreateRequest();

        await provider.ProcessPaymentAsync(request);

        var conflictingRequest = request with
        {
            CustomerId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.ProcessPaymentAsync(conflictingRequest));
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldRejectDifferentAmountForSameKey()
    {
        var provider = new PaymentProvider();
        var request = CreateRequest();

        await provider.ProcessPaymentAsync(request);

        var conflictingRequest = request with
        {
            Amount = request.Amount + 10m
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.ProcessPaymentAsync(conflictingRequest));
    }

    [Fact]
    public async Task GetPaymentAsync_ShouldReturnNullWhenPaymentDoesNotExist()
    {
        var provider = new PaymentProvider();

        var result = await provider.GetPaymentAsync(
            Guid.NewGuid().ToString("N"));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPaymentAsync_ShouldReturnPreviouslyProcessedPayment()
    {
        var provider = new PaymentProvider();
        var request = CreateRequest();

        var processed = await provider.ProcessPaymentAsync(request);

        var recovered = await provider.GetPaymentAsync(
            request.IdempotencyKey);

        Assert.NotNull(recovered);
        Assert.Equal(processed, recovered);
        Assert.Equal(processed.ExternalPaymentId, recovered.ExternalPaymentId);
    }

    private static PaymentRequest CreateRequest()
    {
        var invoiceId = Guid.NewGuid();

        return new PaymentRequest(
            invoiceId,
            Guid.NewGuid(),
            125.50m,
            invoiceId.ToString("N"));
    }
}
