namespace Comven_VehicleBilling.Infrastructure.BillingProvider;

public sealed class BillingProvider : IBillingProvider
{
  public Task<BillingProviderResult> GenerateBillingAsync(
      BillingProviderRequest request,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (request.CustomerId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador do cliente é obrigatório.",
          nameof(request));
    }

    if (request.OperationId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador da operação é obrigatório.",
          nameof(request));
    }

    if (request.Amount <= 0)
    {
      throw new ArgumentOutOfRangeException(
          nameof(request),
          "O valor do faturamento deve ser maior que zero.");
    }

    var externalId = $"SIM-{Guid.NewGuid():N}";

    var result = new BillingProviderResult(
        Success: true,
        ExternalId: externalId,
        ErrorMessage: null);

    return Task.FromResult(result);
  }
}
