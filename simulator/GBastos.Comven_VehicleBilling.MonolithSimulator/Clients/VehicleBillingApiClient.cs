using System.Globalization;
using System.Net.Http.Json;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;
using Comven_VehicleBilling.MonolithSimulator.Models.Responses;

namespace Comven_VehicleBilling.MonolithSimulator.Clients;

internal sealed class VehicleBillingApiClient
{
  private readonly HttpClient _httpClient;

  public VehicleBillingApiClient(HttpClient httpClient)
  {
    _httpClient = httpClient
        ?? throw new ArgumentNullException(nameof(httpClient));
  }

  public async Task<IReadOnlyList<CustomerResponse>> GetCustomersAsync(
    CancellationToken cancellationToken = default)
  {
    var customers = await _httpClient.GetFromJsonAsync<List<CustomerResponse>>(
        "api/customers/",
        cancellationToken);

    return customers ?? [];
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

    if (customerId == Guid.Empty)
      throw new ArgumentException(
          "O identificador do cliente é obrigatório.",
          nameof(customerId));

    if (amount <= 0)
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor deve ser maior que zero.");

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

  public Task<HttpResponseMessage> GetOperationsAsync(
      Guid? customerId = null,
      int page = 1,
      int pageSize = 50,
      CancellationToken cancellationToken = default,
      string? operationType = null,
      DateTimeOffset? startDate = null,
      DateTimeOffset? endDate = null,
      string? status = null)
  {
    if (customerId.HasValue && customerId.Value == Guid.Empty)
      throw new ArgumentException(
          "O identificador do cliente é inválido.",
          nameof(customerId));

    if (page < 1)
      throw new ArgumentOutOfRangeException(nameof(page));

    if (pageSize < 1 || pageSize > 100)
      throw new ArgumentOutOfRangeException(nameof(pageSize));

    var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

    if (customerId.HasValue)
      query.Add($"customerId={customerId.Value}");

    if (!string.IsNullOrWhiteSpace(operationType))
      query.Add($"operationType={Uri.EscapeDataString(operationType)}");

    if (startDate.HasValue)
      query.Add(
          $"startDate={Uri.EscapeDataString(startDate.Value.ToString("O", CultureInfo.InvariantCulture))}");

    if (endDate.HasValue)
      query.Add(
          $"endDate={Uri.EscapeDataString(endDate.Value.ToString("O", CultureInfo.InvariantCulture))}");

    if (!string.IsNullOrWhiteSpace(status))
      query.Add($"status={Uri.EscapeDataString(status)}");

    var url = $"api/operations?{string.Join("&", query)}";

    return _httpClient.GetAsync(url, cancellationToken);
  }

  public Task<HttpResponseMessage> GetOperationByIdAsync(
      Guid operationId,
      CancellationToken cancellationToken = default)
  {
    if (operationId == Guid.Empty)
      throw new ArgumentException(
          "O identificador da operação é obrigatório.",
          nameof(operationId));

    return _httpClient.GetAsync(
        $"api/operations/{operationId}",
        cancellationToken);
  }

  public Task<HttpResponseMessage> RefundOperationAsync(
      Guid operationId,
      CancellationToken cancellationToken = default)
  {
    if (operationId == Guid.Empty)
      throw new ArgumentException(
          "O identificador da operação é obrigatório.",
          nameof(operationId));

    return _httpClient.PostAsync(
        $"api/operations/{operationId}/refund",
        content: null,
        cancellationToken);
  }
  public Task<HttpResponseMessage> GetStatementAsync(
      Guid customerId,
      DateOnly startDate,
      DateOnly endDate,
      CancellationToken cancellationToken = default)
  {
    return _httpClient.GetAsync(
        $"api/statements/{customerId}" +
        $"?startDate={startDate:yyyy-MM-dd}" +
        $"&endDate={endDate:yyyy-MM-dd}",
        cancellationToken);
  }

  public Task<HttpResponseMessage> GetStatementEntriesAsync(
      Guid customerId,
      int page = 1,
      int pageSize = 50,
      CancellationToken cancellationToken = default)
  {
    return _httpClient.GetAsync(
        $"api/statements/{customerId}/entries" +
        $"?page={page}&pageSize={pageSize}",
        cancellationToken);
  }

}
