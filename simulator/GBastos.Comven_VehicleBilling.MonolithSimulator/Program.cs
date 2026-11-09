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
      "Informe uma URL HTTP ou HTTPS vÃ¡lida.");

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
        "A API nÃ£o estÃ¡ saudÃ¡vel. Verifique o serviÃ§o.");

    return 1;
  }

  Console.WriteLine();
  Console.WriteLine("API disponÃ­vel.");

  while (true)
  {
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine(" MENU PRINCIPAL");
    Console.WriteLine("========================================");
    Console.WriteLine();
    Console.WriteLine("1 - Executar cenÃ¡rio de registro de operaÃ§Ãµes");
    Console.WriteLine("2 - Estornar operação");
    Console.WriteLine("3 - Gerar fatura pós-paga");
    Console.WriteLine("4 - Consultar fatura");
    Console.WriteLine("0 - Encerrar");
    Console.WriteLine();

    Console.Write("OpÃ§Ã£o: ");

    var option = Console.ReadLine()?.Trim();

    switch (option)
    {
      case "1":
        await OperationScenarios.ExecuteAsync(apiClient);
        break;

      case "2":
        await OperationScenarios.RefundAsync(apiClient);
        break;

      case "3":
        await InvoiceScenarios.CreateAsync(apiClient);
        break;

      case "4":
        await InvoiceScenarios.GetAsync(apiClient);
        break;

      case "0":
        Console.WriteLine("Simulador encerrado.");
        return 0;

      default:
        Console.WriteLine("OpÃ§Ã£o invÃ¡lida.");
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
      $"Erro de comunicaÃ§Ã£o com a API: {ex.Message}");

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
      $"Resposta JSON invÃ¡lida: {ex.Message}");

  return 1;
}
catch (Exception ex)
{
  Console.Error.WriteLine(
      $"Erro durante a execuÃ§Ã£o: {ex.Message}");

  return 1;
}


