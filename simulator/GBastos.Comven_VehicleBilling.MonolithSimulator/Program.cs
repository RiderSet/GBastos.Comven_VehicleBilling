using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Scenarios;

var baseUrl = args.Length > 0
    ? args[0].TrimEnd('/')
    : "http://localhost:5237";

if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiUri) ||
    (apiUri.Scheme != Uri.UriSchemeHttp &&
     apiUri.Scheme != Uri.UriSchemeHttps))
{
  Console.Error.WriteLine(
      "Informe uma URL HTTP ou HTTPS válida.");

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

  while (true)
  {
    Console.WriteLine();
    Console.WriteLine("Selecione uma opção:");
    Console.WriteLine();
    Console.WriteLine("1 - Registrar operação");
    Console.WriteLine("2 - Consultar operações");
    Console.WriteLine("3 - Consultar operação por ID");
    Console.WriteLine("4 - Executar cenário completo");
    Console.WriteLine("0 - Encerrar");
    Console.WriteLine();

    Console.Write("Opção: ");

    var option = Console.ReadLine()?.Trim();

    switch (option)
    {
      case "1":
        await scenario.RegisterOperationAsync(apiClient);
        break;

      case "2":
        await scenario.ListOperationsAsync(apiClient);
        break;

      case "3":
        await scenario.GetOperationDetailsAsync(apiClient);
        break;

      case "4":
        await scenario.ExecuteAsync(apiClient);
        break;

      case "0":
        Console.WriteLine("Simulador encerrado.");
        return 0;

      default:
        Console.Error.WriteLine("Opção inválida.");
        break;
    }

    Console.WriteLine();
    Console.WriteLine("Pressione ENTER para voltar ao menu...");
    Console.ReadLine();
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
