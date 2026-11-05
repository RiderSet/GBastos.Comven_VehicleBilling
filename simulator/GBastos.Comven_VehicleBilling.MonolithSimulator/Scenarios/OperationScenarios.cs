using System.Globalization;
using System.Text.Json;
using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;

namespace Comven_VehicleBilling.MonolithSimulator.Scenarios;

internal sealed class OperationScenarios
{
  private const int DefaultPage = 1;
  private const int DefaultPageSize = 50;
  private const int MaxPageSize = 100;

  public async Task<int> ExecuteAsync(
      VehicleBillingApiClient apiClient)
  {
    ArgumentNullException.ThrowIfNull(apiClient);

    // 1. Cadastro do cliente.
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
      Console.Error.WriteLine(
          "Todos os dados do cliente são obrigatórios.");

      return 1;
    }

    using var customerResponse =
        await apiClient.CreateCustomerAsync(
            name, document, email);

    var customerBody =
        await customerResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"Cadastro: {(int)customerResponse.StatusCode} " +
        customerResponse.StatusCode);

    if (!customerResponse.IsSuccessStatusCode)
    {
      Console.Error.WriteLine(customerBody);
      return 1;
    }

    using var customerJson =
        JsonDocument.Parse(customerBody);

    if (!customerJson.RootElement.TryGetProperty(
            "id", out var idElement) ||
        !Guid.TryParse(
            idElement.GetString(), out var customerId))
    {
      Console.Error.WriteLine(
          "A API não retornou um ID de cliente válido.");

      Console.Error.WriteLine(customerBody);
      return 1;
    }

    Console.WriteLine($"Cliente criado: {customerId}");

    // 2. Crédito de saldo pré-pago.
    Console.WriteLine();
    Console.WriteLine("=== Crédito de saldo pré-pago ===");

    Console.Write("Valor do crédito (ex.: 100,00): ");
    var creditText = Console.ReadLine()?.Trim();

    if (!decimal.TryParse(
            creditText,
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"),
            out var creditAmount) ||
        creditAmount <= 0)
    {
      Console.Error.WriteLine(
          "Informe um valor de crédito maior que zero.");

      return 1;
    }

    var externalCreditReference =
        Guid.NewGuid().ToString();

    using var creditResponse =
        await apiClient.CreditBalanceAsync(
            customerId,
            externalCreditReference,
            creditAmount);

    var creditBody =
        await creditResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"Crédito: {(int)creditResponse.StatusCode} " +
        creditResponse.StatusCode);

    if (!creditResponse.IsSuccessStatusCode)
    {
      Console.Error.WriteLine(creditBody);

      Console.Error.WriteLine(
          "Não foi possível creditar o saldo.");

      return 1;
    }

    Console.WriteLine(
        $"Saldo creditado: {creditAmount:C}");

    // 3. Registro da operação.
    Console.WriteLine();
    Console.WriteLine("=== Registro da operação ===");

    Console.WriteLine(
        "Tipos sugeridos: VehicleConsultation, " +
        "CrlveIssuance, PlateConsultation");

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
          "Informe um tipo e um valor maior que zero.");

      return 1;
    }

    if (amount > creditAmount)
    {
      Console.Error.WriteLine(
          "O valor da operação excede o crédito informado.");

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

    using var operationJson =
        JsonDocument.Parse(operationBody);

    Console.WriteLine("Operação registrada:");

    Console.WriteLine(
        JsonSerializer.Serialize(
            operationJson.RootElement,
            new JsonSerializerOptions
            {
              WriteIndented = true
            }));

    if (!operationJson.RootElement.TryGetProperty(
            "id", out var operationIdElement) ||
        !Guid.TryParse(
            operationIdElement.GetString(),
            out var originalId))
    {
      Console.Error.WriteLine(
          "A API não retornou um ID de operação válido.");

      return 1;
    }

    // 4. Teste de idempotência.
    Console.WriteLine();
    Console.WriteLine("=== Teste de idempotência ===");

    Console.WriteLine(
        "Reenviando a mesma operação...");

    using var retryResponse =
        await apiClient.CreateOperationAsync(request);

    var retryBody =
        await retryResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"Retentativa: {(int)retryResponse.StatusCode} " +
        retryResponse.StatusCode);

    if (!retryResponse.IsSuccessStatusCode)
    {
      Console.Error.WriteLine(retryBody);

      Console.Error.WriteLine(
          "FALHA: a API rejeitou a retentativa.");

      return 1;
    }

    using var retryJson =
        JsonDocument.Parse(retryBody);

    if (!retryJson.RootElement.TryGetProperty(
            "id", out var retryIdElement) ||
        !Guid.TryParse(
            retryIdElement.GetString(),
            out var retryId))
    {
      Console.Error.WriteLine(
          "A retentativa não retornou um ID válido.");

      return 1;
    }

    if (originalId != retryId)
    {
      Console.Error.WriteLine(
          "FALHA: a retentativa retornou outra operação.");

      Console.Error.WriteLine($"Original: {originalId}");
      Console.Error.WriteLine($"Retentativa: {retryId}");

      return 1;
    }

    Console.WriteLine();
    Console.WriteLine("OK: idempotência confirmada.");

    Console.WriteLine($"OperationId: {originalId}");
    Console.WriteLine(
        $"ExternalOperationId: {externalOperationId}");

    // 5. Consulta das operações registradas.
    Console.WriteLine();
    Console.WriteLine("=== Registro de operações: consulta ===");

    using var operationsResponse =
        await apiClient.GetOperationsAsync(
            customerId,
            DefaultPage,
            DefaultPageSize);

    var operationsBody =
        await operationsResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"Consulta: {(int)operationsResponse.StatusCode} " +
        operationsResponse.StatusCode);

    if (!operationsResponse.IsSuccessStatusCode)
    {
      Console.Error.WriteLine(operationsBody);

      Console.Error.WriteLine(
          "Não foi possível consultar as operações.");

      return 1;
    }

    using var operationsJson =
        JsonDocument.Parse(operationsBody);

    if (!TryDisplayOperations(
            operationsJson.RootElement,
            customerId,
            DefaultPage,
            DefaultPageSize))
    {
      Console.Error.WriteLine(
          "A API retornou um formato inesperado na listagem.");

      Console.Error.WriteLine(operationsBody);
      return 1;
    }

    Console.WriteLine();
    Console.WriteLine(
        "Registro e consulta de operações concluídos com sucesso.");

    return 0;
  }

  public async Task<int> ListOperationsAsync(
      VehicleBillingApiClient apiClient)
  {
    ArgumentNullException.ThrowIfNull(apiClient);

    Console.WriteLine();
    Console.WriteLine("=== Consulta independente de operações ===");

    Console.Write(
        "ID do cliente (Enter para consultar todos): ");

    var customerIdText = Console.ReadLine()?.Trim();

    Guid? customerId = null;

    if (!string.IsNullOrWhiteSpace(customerIdText))
    {
      if (!Guid.TryParse(customerIdText, out var parsedId) ||
          parsedId == Guid.Empty)
      {
        Console.Error.WriteLine(
            "Informe um GUID de cliente válido.");

        return 1;
      }

      customerId = parsedId;
    }

    Console.Write(
        $"Página (padrão {DefaultPage}): ");

    var pageText = Console.ReadLine()?.Trim();

    var page = DefaultPage;

    if (!string.IsNullOrWhiteSpace(pageText) &&
        (!int.TryParse(
            pageText,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out page) ||
         page < 1))
    {
      Console.Error.WriteLine(
          "A página deve ser um número inteiro maior que zero.");

      return 1;
    }

    Console.Write(
        $"Tamanho da página (padrão {DefaultPageSize}, máximo {MaxPageSize}): ");

    var pageSizeText = Console.ReadLine()?.Trim();

    var pageSize = DefaultPageSize;

    if (!string.IsNullOrWhiteSpace(pageSizeText) &&
        (!int.TryParse(
            pageSizeText,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out pageSize) ||
         pageSize < 1 ||
         pageSize > MaxPageSize))
    {
      Console.Error.WriteLine(
          $"O tamanho da página deve estar entre 1 e {MaxPageSize}.");

      return 1;
    }

    using var response =
        await apiClient.GetOperationsAsync(
            customerId,
            page,
            pageSize);

    var body =
        await response.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine(
        $"Consulta: {(int)response.StatusCode} {response.StatusCode}");

    if (!response.IsSuccessStatusCode)
    {
      Console.Error.WriteLine(body);
      return 1;
    }

    using var json = JsonDocument.Parse(body);

    if (!TryDisplayOperations(
            json.RootElement,
            customerId,
            page,
            pageSize))
    {
      Console.Error.WriteLine(
          "A API retornou um formato inesperado na listagem.");

      Console.Error.WriteLine(body);
      return 1;
    }

    return 0;
  }

  public async Task<int> GetOperationDetailsAsync(
    VehicleBillingApiClient apiClient)
  {
    ArgumentNullException.ThrowIfNull(apiClient);

    Console.WriteLine();
    Console.WriteLine("=== Detalhamento de operação ===");

    Console.Write("ID da operação: ");
    var operationIdText = Console.ReadLine()?.Trim();

    if (!Guid.TryParse(operationIdText, out var operationId) ||
        operationId == Guid.Empty)
    {
      Console.Error.WriteLine(
          "Informe um identificador de operação válido.");

      return 1;
    }

    using var response =
        await apiClient.GetOperationByIdAsync(operationId);

    var body =
        await response.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine(
        $"Consulta: {(int)response.StatusCode} {response.StatusCode}");

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      Console.WriteLine(
          "Nenhuma operação foi encontrada com o ID informado.");

      return 0;
    }

    if (!response.IsSuccessStatusCode)
    {
      Console.Error.WriteLine(body);

      Console.Error.WriteLine(
          "Não foi possível consultar os detalhes da operação.");

      return 1;
    }

    using var json = JsonDocument.Parse(body);

    if (json.RootElement.ValueKind != JsonValueKind.Object)
    {
      Console.Error.WriteLine(
          "A API retornou um formato inesperado.");

      Console.Error.WriteLine(body);
      return 1;
    }

    Console.WriteLine();
    Console.WriteLine("=== Dados da operação ===");

    Console.WriteLine(
        JsonSerializer.Serialize(
            json.RootElement,
            new JsonSerializerOptions
            {
              WriteIndented = true
            }));

    Console.WriteLine();
    Console.WriteLine(
        "Detalhamento da operação concluído com sucesso.");

    return 0;
  }

  private static bool TryDisplayOperations(
      JsonElement operations,
      Guid? customerId,
      int page,
      int pageSize)
  {
    JsonElement items;

    if (operations.ValueKind == JsonValueKind.Array)
    {
      items = operations;
    }
    else if (operations.ValueKind == JsonValueKind.Object &&
             operations.TryGetProperty("items", out var resultItems) &&
             resultItems.ValueKind == JsonValueKind.Array)
    {
      items = resultItems;
    }
    else
    {
      return false;
    }

    Console.WriteLine();
    Console.WriteLine(
        $"Cliente: {(customerId.HasValue ? customerId.Value.ToString() : "Todos")}");

    Console.WriteLine($"Página: {page}");
    Console.WriteLine($"Tamanho da página: {pageSize}");
    Console.WriteLine($"Operações encontradas: {items.GetArrayLength()}");

    foreach (var operation in items.EnumerateArray())
    {
      var id = ReadString(operation, "id");
      var type = ReadString(operation, "operationType");
      var operationCustomerId = ReadString(operation, "customerId");

      var operationAmount =
          operation.TryGetProperty("amount", out var amountElement)
              ? amountElement.ToString()
              : "N/D";

      var createdAt = ReadString(operation, "createdAt");

      Console.WriteLine();
      Console.WriteLine("----------------------------------------");
      Console.WriteLine($"ID: {id}");
      Console.WriteLine($"Cliente: {operationCustomerId}");
      Console.WriteLine($"Tipo: {type}");
      Console.WriteLine($"Valor: {operationAmount}");
      Console.WriteLine($"Data: {createdAt}");
    }

    return true;
  }

  private static string ReadString(
      JsonElement element,
      string propertyName)
  {
    if (!element.TryGetProperty(
            propertyName, out var property))
    {
      return "N/D";
    }

    return property.ValueKind == JsonValueKind.String
        ? property.GetString() ?? "N/D"
        : property.ToString();
  }

}
