namespace Comven_VehicleBilling.Infrastructure.BillingsProvider;

public interface IBillingProvider
{
  Task<BillingProviderResult> GenerateBillingAsync(
      BillingProviderRequest request,
      CancellationToken cancellationToken = default);
}

public sealed record BillingProviderRequest(
    Guid CustomerId,
    Guid OperationId,
    decimal Amount,
    DateOnly ReferenceDate);

public sealed record BillingProviderResult(
    bool Success,
    string? ExternalId,
    string? ErrorMessage);
