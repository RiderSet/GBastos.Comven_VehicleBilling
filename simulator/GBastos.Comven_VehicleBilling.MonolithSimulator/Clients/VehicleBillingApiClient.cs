using System.Net.Http.Json;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;

namespace Comven_VehicleBilling.MonolithSimulator.Clients;

internal sealed class VehicleBillingApiClient
{
    private readonly HttpClient _httpClient;

    public VehicleBillingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Task<HttpResponseMessage> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        return _httpClient.GetAsync("health", cancellationToken);
    }

    public Task<HttpResponseMessage> CreateCustomerAsync(
        string name,
        string document,
        string email,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            Name = name,
            Document = document,
            Email = email
        };

        return _httpClient.PostAsJsonAsync(
            "api/customers",
            request,
            cancellationToken);
    }

    public Task<HttpResponseMessage> CreateOperationAsync(
        CreateOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _httpClient.PostAsJsonAsync(
            "api/operations",
            request,
            cancellationToken);
    }

public Task<HttpResponseMessage> CreditBalanceAsync(
    Guid customerId,
    string externalReference,
    decimal amount,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);

    if (amount <= 0)
        throw new ArgumentOutOfRangeException(nameof(amount));

    var request = new
    {
        ExternalReference = externalReference,
        Amount = amount
    };

    return _httpClient.PostAsJsonAsync(
        $"api/customers/{customerId}/balance/credits",
        request,
        cancellationToken);
}
}