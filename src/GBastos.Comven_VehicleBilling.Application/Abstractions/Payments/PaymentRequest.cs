namespace Comven_VehicleBilling.Application.Abstractions.Payments;

public sealed record PaymentRequest(
    Guid InvoiceId,
    Guid CustomerId,
    decimal Amount,
    string IdempotencyKey);

public sealed record PaymentResult(
    PaymentOutcome Outcome,
    string? ExternalPaymentId,
    string? ErrorMessage);
