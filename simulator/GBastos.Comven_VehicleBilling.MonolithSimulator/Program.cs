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
  BaseAddress = new Uri(apiUri.AbsoluteUri.TrimEnd('/') + "/"),
  Timeout = TimeSpan.FromSeconds(30)
};

var apiClient = new VehicleBillingApiClient(httpClient);

Console.WriteLine("========================================");
Console.WriteLine(" MonolithSimulator - VehicleBilling");
Console.WriteLine("========================================");
Console.WriteLine($"API: {apiUri}");

try
{
  using var health = await apiClient.GetHealthAsync();

  Console.WriteLine(
      $"Health: {(int)health.StatusCode} {health.StatusCode}");

  if (!health.IsSuccessStatusCode)
  {
    Console.Error.WriteLine(
        "A API não está saudável. Verifique o serviço.");

    return 1;
  }

  Console.WriteLine();
  Console.WriteLine("API disponível.");

  while (true)
  {
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine(" MENU PRINCIPAL");
    Console.WriteLine("========================================");
    Console.WriteLine();
    Console.WriteLine("1 - Executar cenário de registro de operações");
    Console.WriteLine("0 - Encerrar");
    Console.WriteLine();

    Console.Write("Opção: ");

    var option = Console.ReadLine()?.Trim();

    switch (option)
    {
      case "1":
        await OperationScenarios.ExecuteAsync(apiClient);
        break;

      case "0":
        Console.WriteLine("Simulador encerrado.");
        return 0;

      default:
        Console.WriteLine("Opção inválida.");
        continue;
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
catch (Exception ex)
{
  Console.Error.WriteLine(
      $"Erro durante a execução: {ex.Message}");

  return 1;
}
