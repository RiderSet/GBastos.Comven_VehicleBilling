using System.Globalization;
using System.Text.Json;
using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;

namespace Comven_VehicleBilling.MonolithSimulator.Scenarios;

internal sealed class OperationScenarios
{
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
    Console.WriteLine(
        "OK: idempotência confirmada.");

    Console.WriteLine($"OperationId: {originalId}");
    Console.WriteLine(
        $"ExternalOperationId: {externalOperationId}");

    Console.WriteLine(
        "Registro de operação concluído com sucesso.");

    return 0;
  }
}
