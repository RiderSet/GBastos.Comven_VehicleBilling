using Comven_VehicleBilling.Application.Abstractions.Billing;

namespace Comven_VehicleBilling.Infrastructure.BillingsProvider;

public sealed class BillingProvider : IBillingProvider
{
    public Task<BillingProviderResult> GenerateBillingAsync(
        BillingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.InvoiceId == Guid.Empty)
            throw new ArgumentException(
                "O identificador da fatura é obrigatório.",
                nameof(request));

        if (request.CustomerId == Guid.Empty)
            throw new ArgumentException(
                "O identificador do cliente é obrigatório.",
                nameof(request));

        if (request.Amount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "O valor do faturamento deve ser maior que zero.");

        var expectedKey = request.InvoiceId.ToString("N");

        if (!string.Equals(
                request.IdempotencyKey,
                expectedKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A chave de idempotência deve corresponder ao identificador da fatura.",
                nameof(request));
        }

        var result = new BillingProviderResult(
            Success: true,
            ExternalId: $"SIM-{expectedKey}",
            ErrorMessage: null);

        return Task.FromResult(result);
    }
}
