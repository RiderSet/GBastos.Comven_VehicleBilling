using Comven_VehicleBilling.MonolithSimulator.Models.Requests;
using System.Net.Http.Json;

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
}
