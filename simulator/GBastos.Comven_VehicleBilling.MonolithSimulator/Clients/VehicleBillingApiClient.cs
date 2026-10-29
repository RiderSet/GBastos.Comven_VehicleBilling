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
    return _httpClient.GetAsync(
        "health",
        cancellationToken);
  }
}
