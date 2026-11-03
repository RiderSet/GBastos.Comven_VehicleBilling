using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;

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
Console.WriteLine(" MonolithSimulator - Registro de operação");
Console.WriteLine("========================================");
Console.WriteLine($"API: {baseUrl}");
Console.WriteLine();

try
{
    using (var health = await apiClient.GetHealthAsync())
    {
        Console.WriteLine(
            $"Health: {(int)health.StatusCode} {health.StatusCode}");

        if (!health.IsSuccessStatusCode)
        {
            Console.Error.WriteLine(
                "A API não está saudável. Operação cancelada.");
            return 1;
        }
    }

    Console.WriteLine();
    Console.WriteLine("=== Cadastro do cliente ===");

    Console.Write("Nome: ");
    var name = Console.ReadLine()?.Trim();

    Console.Write("Documento: ");
    var document = Console.ReadLine()?.Trim();

    Console.Write("E-mail: ");
    var email = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(name) ||
        string.IsNullOrWhiteSpace(document) ||
        string.IsNullOrWhiteSpace(email))
    {
        Console.Error.WriteLine("Todos os dados do cliente são obrigatórios.");
        return 1;
    }

    using var customerResponse = await apiClient.CreateCustomerAsync(
        name, document, email);

    var customerBody =
        await customerResponse.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine(
        $"Cadastro: {(int)customerResponse.StatusCode} " +
        customerResponse.StatusCode);

    if (!customerResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine(customerBody);
        return 1;
    }

    using var customerJson = JsonDocument.Parse(customerBody);

    if (!customerJson.RootElement.TryGetProperty("id", out var idElement) ||
        !Guid.TryParse(idElement.GetString(), out var customerId))
    {
        Console.Error.WriteLine(
            "A API não retornou um ID de cliente válido.");
        Console.Error.WriteLine(customerBody);
        return 1;
    }

    Console.WriteLine($"Cliente criado: {customerId}");

    Console.WriteLine();
    Console.WriteLine("=== Registro da operação ===");
    Console.WriteLine("Tipos sugeridos: VehicleConsultation, CrlveIssuance, PlateConsultation");

    Console.Write("Tipo da operação: ");
    var operationType = Console.ReadLine()?.Trim();

    Console.Write("Valor (ex.: 25,90): ");
    var amountText = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(operationType) ||
        !decimal.TryParse(
            amountText,
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"),
            out var amount) ||
        amount <= 0)
    {
        Console.Error.WriteLine(
            "Informe um tipo e um valor numérico maior que zero.");
        return 1;
    }

    var externalOperationId = Guid.NewGuid();

    var request = new CreateOperationRequest(
        externalOperationId,
        customerId,
        operationType,
        amount);

    using var operationResponse =
        await apiClient.CreateOperationAsync(request);

    var operationBody =
        await operationResponse.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine(
        $"Operação: {(int)operationResponse.StatusCode} " +
        operationResponse.StatusCode);

    if (!operationResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine(operationBody);
        return 1;
    }

    Console.WriteLine("Operação registrada:");
    using var operationJson = JsonDocument.Parse(operationBody);

    Console.WriteLine(
        JsonSerializer.Serialize(
            operationJson.RootElement,
            new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine();
    Console.WriteLine($"ExternalOperationId: {externalOperationId}");
    Console.WriteLine("Fluxo concluído com sucesso.");

    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Erro de comunicação com a API: {ex.Message}");
    return 1;
}
catch (TaskCanceledException)
{
    Console.Error.WriteLine("A chamada HTTP foi cancelada ou excedeu o tempo limite.");
    return 1;
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"Resposta JSON inválida: {ex.Message}");
    return 1;
}