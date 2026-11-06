using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;
using Comven_VehicleBilling.MonolithSimulator.Models.Responses;
using System.Net.Http;
using System.Text.Json;

namespace Comven_VehicleBilling.MonolithSimulator.Scenarios;

internal static class OperationScenarios
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public static async Task ExecuteAsync(
      VehicleBillingApiClient client,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(client);

    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine(" CENÁRIO: REGISTRO DE OPERAÇÕES");
    Console.WriteLine("======================================");

    // 1. Criar cliente
    Console.WriteLine("\n[1/5] Criando cliente...");

    var customerResponse = await client.CreateCustomerAsync(
        "Cliente Simulador",
        "12345678909",
        $"simulador-{Guid.NewGuid():N}@comven.com.br",
        cancellationToken);

    await EnsureSuccessAsync(customerResponse, "Criação do cliente");

    var customerJson = await customerResponse.Content.ReadAsStringAsync(
        cancellationToken);

    using var customerDocument = JsonDocument.Parse(customerJson);

    if (!customerDocument.RootElement.TryGetProperty(
            "id", out var customerIdElement) ||
        !Guid.TryParse(customerIdElement.GetString(), out var customerId))
    {
      throw new InvalidOperationException(
          "A API não retornou um ID válido para o cliente.");
    }

    Console.WriteLine($"Cliente criado: {customerId}");

    // 2. Creditar saldo
    Console.WriteLine("\n[2/5] Creditando saldo...");

    var creditReference = Guid.NewGuid().ToString("N");

    var creditResponse = await client.CreditBalanceAsync(
        customerId,
        creditReference,
        1000.00m,
        cancellationToken);

    await EnsureSuccessAsync(creditResponse, "Crédito de saldo");

    Console.WriteLine("Crédito de R$ 1.000,00 realizado.");

    // 3. Criar operação
    Console.WriteLine("\n[3/5] Registrando operação...");

    var externalOperationId = Guid.NewGuid();

    var request = new CreateOperationRequest(
        externalOperationId,
        customerId,
        "VehicleConsultation",
        25.00m);

    var operationResponse = await client.CreateOperationAsync(
        request,
        cancellationToken);

    await EnsureSuccessAsync(operationResponse, "Registro da operação");

    var operationJson = await operationResponse.Content.ReadAsStringAsync(
        cancellationToken);

    var operation = JsonSerializer.Deserialize<OperationResponse>(
        operationJson,
        JsonOptions);

    if (operation is null || operation.Id == Guid.Empty)
    {
      throw new InvalidOperationException(
          "A API não retornou uma operação válida.");
    }

    Console.WriteLine($"Operação registrada: {operation.Id}");
    Console.WriteLine($"Cliente: {operation.CustomerId}");
    Console.WriteLine($"Tipo: {operation.OperationType}");
    Console.WriteLine($"Valor: R$ {operation.Amount:N2}");

    // 4. Reenviar a mesma operação para testar idempotência
    Console.WriteLine("\n[4/5] Testando idempotência...");

    var retryResponse = await client.CreateOperationAsync(
        request,
        cancellationToken);

    await EnsureSuccessAsync(retryResponse, "Reenvio da operação");

    var retryJson = await retryResponse.Content.ReadAsStringAsync(
        cancellationToken);

    var retryOperation = JsonSerializer.Deserialize<OperationResponse>(
        retryJson,
        JsonOptions);

    if (retryOperation is null ||
        retryOperation.Id != operation.Id)
    {
      throw new InvalidOperationException(
          "Falha na idempotência: o reenvio gerou uma operação diferente.");
    }

    Console.WriteLine(
        "Idempotência confirmada: o reenvio retornou a mesma operação.");

    // 5. Consultar operações do cliente
    Console.WriteLine("\n[5/5] Consultando operações...");

    var listResponse = await client.GetOperationsAsync(
        customerId,
        1,
        50,
        cancellationToken);

    await EnsureSuccessAsync(listResponse, "Consulta de operações");

    var listJson = await listResponse.Content.ReadAsStringAsync(
        cancellationToken);

    using var listDocument = JsonDocument.Parse(listJson);

    Console.WriteLine("Consulta realizada com sucesso.");
    Console.WriteLine($"Resposta da API: {listJson}");

    Console.WriteLine("\n======================================");
    Console.WriteLine(" CENÁRIO CONCLUÍDO COM SUCESSO");
    Console.WriteLine("======================================");
  }

  private static async Task EnsureSuccessAsync(
      HttpResponseMessage response,
      string operation)
  {
    if (response.IsSuccessStatusCode)
      return;

    var content = await response.Content.ReadAsStringAsync();

    throw new HttpRequestException(
        $"{operation} falhou. " +
        $"HTTP {(int)response.StatusCode} ({response.StatusCode}). " +
        $"Resposta: {content}");
  }
}
