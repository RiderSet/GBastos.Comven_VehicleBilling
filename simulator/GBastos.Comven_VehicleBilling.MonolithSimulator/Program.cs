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

  Console.WriteLine();
  Console.WriteLine("Selecione uma opção:");
  Console.WriteLine("1 - Executar cenário completo");
  Console.WriteLine("2 - Consultar operações");
  Console.WriteLine("3 - Detalhar operação pelo ID");
  Console.WriteLine("0 - Sair");
  Console.WriteLine();

  Console.Write("Opção: ");

  var option = Console.ReadLine()?.Trim();

  switch (option)
  {
    case "1":
      {
        var scenario = new OperationScenarios();
        return await scenario.ExecuteAsync(apiClient);
      }

    case "2":
      {
        var scenario = new OperationScenarios();
        return await scenario.ListOperationsAsync(apiClient);
      }

    case "3":
      {
        var scenario = new OperationScenarios();
        return await scenario.GetOperationDetailsAsync(apiClient);
      }

    case "0":
      Console.WriteLine("Simulador encerrado.");
      return 0;

    default:
      Console.Error.WriteLine("Opção inválida.");
      return 1;
  }

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
