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
    Console.WriteLine("\n[1/6] Criando cliente...");

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
    Console.WriteLine("\n[2/6] Creditando saldo...");

    var creditReference = Guid.NewGuid().ToString("N");

    var creditResponse = await client.CreditBalanceAsync(
        customerId,
        creditReference,
        1000.00m,
        cancellationToken);

    await EnsureSuccessAsync(creditResponse, "Crédito de saldo");

    Console.WriteLine("Crédito de R$ 1.000,00 realizado.");

    // 3. Registrar operação
    Console.WriteLine("\n[3/6] Registrando operação...");

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

    if (operation.CustomerId != customerId ||
        operation.Amount != 25.00m)
    {
      throw new InvalidOperationException(
          "Os dados retornados da operação não correspondem à solicitação.");
    }

    // 4. Testar idempotência
    Console.WriteLine("\n[4/6] Testando idempotência...");

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
    Console.WriteLine("\n[5/6] Consultando operações...");

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

    // 6. Conciliar saldo e lançamentos do razão
    Console.WriteLine("\n[6/6] Validando saldo e razão...");

    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    var statementResponse = await client.GetStatementAsync(
        customerId,
        today,
        today,
        cancellationToken);

    await EnsureSuccessAsync(
        statementResponse,
        "Consulta do extrato financeiro");

    var statementJson = await statementResponse.Content.ReadAsStringAsync(
        cancellationToken);

    using var statementDocument = JsonDocument.Parse(statementJson);

    var statement = statementDocument.RootElement;

    var totalCredits = statement.GetProperty("totalCredits").GetDecimal();
    var totalDebits = statement.GetProperty("totalDebits").GetDecimal();
    var balance = statement.GetProperty("balance").GetDecimal();

    Console.WriteLine($"Total de créditos: R$ {totalCredits:N2}");
    Console.WriteLine($"Total de débitos: R$ {totalDebits:N2}");
    Console.WriteLine($"Saldo final: R$ {balance:N2}");

    if (totalCredits != 1000.00m ||
        totalDebits != 25.00m ||
        balance != 975.00m)
    {
      throw new InvalidOperationException(
          "Falha na conciliação financeira. " +
          $"Créditos: {totalCredits:N2}; " +
          $"Débitos: {totalDebits:N2}; " +
          $"Saldo: {balance:N2}.");
    }

    // Consultar os lançamentos do razão
    var entriesResponse = await client.GetStatementEntriesAsync(
        customerId,
        1,
        50,
        cancellationToken);

    await EnsureSuccessAsync(
        entriesResponse,
        "Consulta dos lançamentos do razão");

    var entriesJson = await entriesResponse.Content.ReadAsStringAsync(
        cancellationToken);

    using var entriesDocument = JsonDocument.Parse(entriesJson);

    var items = entriesDocument.RootElement.GetProperty("items");

    var operationEntryCount = 0;

    foreach (var entry in items.EnumerateArray())
    {
      if (!entry.TryGetProperty("operationId", out var entryOperationId))
        continue;

      if (entryOperationId.ValueKind != JsonValueKind.String)
        continue;

      if (!Guid.TryParse(
              entryOperationId.GetString(),
              out var parsedOperationId))
        continue;

      if (parsedOperationId != operation.Id)
        continue;

      operationEntryCount++;

      var amount = entry.GetProperty("amount").GetDecimal();

      var type = entry.GetProperty("type").GetString();

      if (amount != 25.00m)
      {
        throw new InvalidOperationException(
            $"Valor incorreto no razão: {amount:N2}.");
      }

      // Confirmar o valor textual do tipo após validar
      // a representação utilizada pelo StatementRepository.
      if (!string.Equals(
              type,
              "Debit",
              StringComparison.OrdinalIgnoreCase))
      {
        throw new InvalidOperationException(
            $"Tipo de lançamento incorreto: {type}.");
      }
    }

    if (operationEntryCount != 1)
    {
      throw new InvalidOperationException(
          "O razão deveria possuir exatamente um lançamento " +
          $"para a operação {operation.Id}, mas foram encontrados " +
          $"{operationEntryCount}.");
    }

    Console.WriteLine(
        "Conciliação financeira confirmada.");

    Console.WriteLine(
        "Lançamento único de R$ 25,00 encontrado no razão.");

    Console.WriteLine("\n======================================");
    Console.WriteLine(" CENÁRIO CONCLUÍDO COM SUCESSO");
    Console.WriteLine("======================================");
  }

  public static async Task RefundAsync(
      VehicleBillingApiClient client,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(client);

    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine(" CENÁRIO: ESTORNO DE OPERAÇÃO");
    Console.WriteLine("======================================");

    Console.WriteLine();
    Console.Write("Informe o ID da operação: ");

    var input = Console.ReadLine()?.Trim();

    if (!Guid.TryParse(input, out var operationId) ||
        operationId == Guid.Empty)
    {
      Console.WriteLine("Identificador de operação inválido.");
      return;
    }

    // Consultar a operação antes do estorno.
    Console.WriteLine();
    Console.WriteLine("Consultando operação...");

    using var operationResponse = await client.GetOperationByIdAsync(
        operationId,
        cancellationToken);

    await EnsureSuccessAsync(
        operationResponse,
        "Consulta da operação");

    var operationJson = await operationResponse.Content.ReadAsStringAsync(
        cancellationToken);

    using var operationDocument = JsonDocument.Parse(operationJson);

    var operation = operationDocument.RootElement;

    Console.WriteLine($"Operação: {operationId}");

    if (operation.TryGetProperty("amount", out var amountElement))
      Console.WriteLine($"Valor: R$ {amountElement.GetDecimal():N2}");

    if (operation.TryGetProperty("status", out var statusElement))
      Console.WriteLine($"Status atual: {statusElement.GetString()}");

    Console.WriteLine();
    Console.Write("Confirma o estorno integral? (S/N): ");

    var confirmation = Console.ReadLine()?.Trim();

    if (!string.Equals(
            confirmation,
            "S",
            StringComparison.OrdinalIgnoreCase))
    {
      Console.WriteLine("Estorno cancelado pelo usuário.");
      return;
    }

    // Executar estorno na API.
    Console.WriteLine();
    Console.WriteLine("Solicitando estorno...");

    using var refundResponse = await client.RefundOperationAsync(
        operationId,
        cancellationToken);

    await EnsureSuccessAsync(
        refundResponse,
        "Estorno da operação");

    var refundJson = await refundResponse.Content.ReadAsStringAsync(
        cancellationToken);

    using var refundDocument = JsonDocument.Parse(refundJson);

    var refundedOperation = refundDocument.RootElement;

    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine(" ESTORNO REALIZADO COM SUCESSO");
    Console.WriteLine("======================================");

    Console.WriteLine($"Operação: {operationId}");

    if (refundedOperation.TryGetProperty(
            "status", out var refundedStatus))
    {
      Console.WriteLine(
          $"Novo status: {refundedStatus.GetString()}");
    }

    if (refundedOperation.TryGetProperty(
            "refundedAt", out var refundedAt) &&
        refundedAt.ValueKind != JsonValueKind.Null)
    {
      Console.WriteLine(
          $"Data do estorno: {refundedAt.GetString()}");
    }

    Console.WriteLine();
    Console.WriteLine("Resposta completa da API:");
    Console.WriteLine(refundJson);
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
