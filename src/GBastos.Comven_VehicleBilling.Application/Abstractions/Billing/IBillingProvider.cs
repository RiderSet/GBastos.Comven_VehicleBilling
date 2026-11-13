namespace Comven_VehicleBilling.Application.Abstractions.Billing;

public interface IBillingProvider
{
    Task<BillingProviderResult> GenerateBillingAsync(
        BillingProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record BillingProviderRequest(
    Guid InvoiceId,
    Guid CustomerId,
    decimal Amount,
    DateOnly ReferenceDate,
    string IdempotencyKey);

public sealed record BillingProviderResult(
    bool Success,
    string? ExternalId,
    string? ErrorMessage);
