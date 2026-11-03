using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Scenarios;

var baseUrl = args.Length > 0
    ? args[0].TrimEnd('/')
    : "http://localhost:5237";

if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiUri) ||
    (apiUri.Scheme != Uri.UriSchemeHttp &&
     apiUri.Scheme != Uri.UriSchemeHttps))
{
  Console.Error.WriteLine("Informe uma URL HTTP ou HTTPS válida.");
  return 1;
}

using var httpClient = new HttpClient
{
  BaseAddress = new Uri(baseUrl + "/"),
  Timeout = TimeSpan.FromSeconds(30)
};

var apiClient = new VehicleBillingApiClient(httpClient);

Console.WriteLine("========================================");
Console.WriteLine(" MonolithSimulator - Registro de operações");
Console.WriteLine("========================================");
Console.WriteLine($"API: {baseUrl}");

try
{
  using var health = await apiClient.GetHealthAsync();

  Console.WriteLine(
      $"Health: {(int)health.StatusCode} {health.StatusCode}");

  if (!health.IsSuccessStatusCode)
  {
    Console.Error.WriteLine("A API não está saudável.");
    return 1;
  }

  var scenario = new OperationScenarios();

  return await scenario.ExecuteAsync(apiClient);
}
catch (HttpRequestException ex)
{
  Console.Error.WriteLine(
      $"Erro de comunicação com a API: {ex.Message}");
  return 1;
}
catch (TaskCanceledException)
{
  Console.Error.WriteLine(
      "A chamada HTTP foi cancelada ou excedeu o tempo limite.");
  return 1;
}
catch (System.Text.Json.JsonException ex)
{
  Console.Error.WriteLine(
      $"Resposta JSON inválida: {ex.Message}");
  return 1;
}
