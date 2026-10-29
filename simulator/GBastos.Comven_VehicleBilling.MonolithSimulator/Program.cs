using System.Text.Json;
using Comven_VehicleBilling.MonolithSimulator.Clients;

var baseUrl = args.Length > 0
    ? args[0].TrimEnd('/')
    : "http://localhost:5237";

if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiUri) ||
    (apiUri.Scheme != Uri.UriSchemeHttp &&
     apiUri.Scheme != Uri.UriSchemeHttps))
{
  Console.Error.WriteLine(
      "URL inválida. Informe uma URL HTTP ou HTTPS válida.");

  return 1;
}

using var httpClient = new HttpClient
{
  BaseAddress = new Uri(baseUrl + "/"),
  Timeout = TimeSpan.FromSeconds(10)
};

var apiClient = new VehicleBillingApiClient(httpClient);

Console.WriteLine("========================================");
Console.WriteLine(" MonolithSimulator");
Console.WriteLine(" Simulação de cliente legado");
Console.WriteLine("========================================");
Console.WriteLine($"API: {baseUrl}");
Console.WriteLine("Operação: GET /health");
Console.WriteLine();

try
{
  using var response = await apiClient.GetHealthAsync();

  var body = await response.Content.ReadAsStringAsync();

  Console.WriteLine(
      $"HTTP: {(int)response.StatusCode} {response.StatusCode}");

  if (!string.IsNullOrWhiteSpace(body))
  {
    try
    {
      using var document = JsonDocument.Parse(body);

      Console.WriteLine(
          JsonSerializer.Serialize(
              document.RootElement,
              new JsonSerializerOptions
              {
                WriteIndented = true
              }));
    }
    catch (JsonException)
    {
      Console.WriteLine(body);
    }
  }

  if (!response.IsSuccessStatusCode)
  {
    Console.Error.WriteLine(
        "A API respondeu, mas indicou uma falha HTTP.");

    return 1;
  }

  Console.WriteLine();
  Console.WriteLine("Teste HTTP concluído com sucesso.");

  return 0;
}
catch (HttpRequestException ex)
{
  Console.Error.WriteLine(
      $"Não foi possível acessar a API: {ex.Message}");

  Console.Error.WriteLine(
      "Confirme se a API está em execução e se a URL está correta.");

  return 1;
}
catch (TaskCanceledException)
{
  Console.Error.WriteLine(
      "A chamada foi cancelada ou excedeu o limite de 10 segundos.");

  return 1;
}
