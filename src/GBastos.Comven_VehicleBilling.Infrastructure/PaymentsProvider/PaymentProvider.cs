using System.Collections.Concurrent;
using Comven_VehicleBilling.Application.Abstractions.Payments;

namespace Comven_VehicleBilling.Infrastructure.PaymentsProvider;

public sealed class PaymentProvider : IPaymentProvider
{
    private readonly ConcurrentDictionary<string, StoredPayment> _payments = new();

    public Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.InvoiceId == Guid.Empty)
            throw new ArgumentException(
                "Invoice id cannot be empty.",
                nameof(request));

        if (request.CustomerId == Guid.Empty)
            throw new ArgumentException(
                "Customer id cannot be empty.",
                nameof(request));

        if (request.Amount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Payment amount must be greater than zero.");

        var expectedKey = request.InvoiceId.ToString("N");

        if (!string.Equals(
                request.IdempotencyKey,
                expectedKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Idempotency key must match the invoice identifier.",
                nameof(request));
        }

        var payment = _payments.GetOrAdd(
            expectedKey,
            _ => new StoredPayment(
                request.CustomerId,
                request.Amount,
                new PaymentResult(
                    Outcome: PaymentOutcome.Approved,
                    ExternalPaymentId: $"PAY-{expectedKey}",
                    ErrorMessage: null)));

        if (payment.CustomerId != request.CustomerId ||
            payment.Amount != request.Amount)
        {
            throw new InvalidOperationException(
                "The idempotency key was already used with different payment data.");
        }

        return Task.FromResult(payment.Result);
    }

    private sealed record StoredPayment(
        Guid CustomerId,
        decimal Amount,
        PaymentResult Result);
}