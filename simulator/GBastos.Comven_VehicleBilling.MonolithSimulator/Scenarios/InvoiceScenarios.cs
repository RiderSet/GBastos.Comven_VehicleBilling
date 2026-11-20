using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;
using Comven_VehicleBilling.MonolithSimulator.Models.Responses;

namespace Comven_VehicleBilling.MonolithSimulator.Scenarios;

internal static class InvoiceScenarios
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public static async Task<Guid?> RegisterAsync(
      VehicleBillingApiClient apiClient)
  {
    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine(" CENÃRIO: FATURAMENTO PÃ“S-PAGO");
    Console.WriteLine("======================================");
    Console.WriteLine();

    var email =
        $"simulador-postpaid-{Guid.NewGuid():N}@comven.com.br";

    var document =
        $"SIM{Guid.NewGuid():N}"[..14];

    Console.WriteLine();
    Console.WriteLine("Criando cliente pÃ³s-pago...");

    using var customerResponse =
        await apiClient.CreateCustomerAsync(
            "Cliente Simulador PÃ³s-Pago",
            document,
            email,
            plan: "Postpaid");

    var customerBody =
        await customerResponse.Content.ReadAsStringAsync();

    if (!customerResponse.IsSuccessStatusCode)
    {
      Console.WriteLine(
          "NÃ£o foi possÃ­vel criar o cliente.");

      Console.WriteLine(customerBody);
      return null;
    }

    var customer =
        JsonSerializer.Deserialize<CustomerResponse>(
            customerBody,
            JsonOptions);

    if (customer is null || customer.Id == Guid.Empty)
    {
      Console.WriteLine(
          "Resposta invÃ¡lida ao criar o cliente.");

      Console.WriteLine(customerBody);
      return null;
    }

    Console.WriteLine($"Cliente criado: {customer.Id}");
    Console.WriteLine("Plano: Postpaid");

    Console.WriteLine();
    Console.WriteLine("Registrando operaÃ§Ã£o pÃ³s-paga...");

    var operationRequest = new CreateOperationRequest(
        Guid.NewGuid(),
        customer.Id,
        "VehicleConsultation",
        12.50m);

    using var operationResponse =
        await apiClient.CreateOperationAsync(operationRequest);

    var operationBody =
        await operationResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"HTTP operaÃ§Ã£o: {(int)operationResponse.StatusCode} " +
        operationResponse.StatusCode);

    if (!operationResponse.IsSuccessStatusCode)
    {
      Console.WriteLine(
          "NÃ£o foi possÃ­vel registrar a operaÃ§Ã£o.");

      Console.WriteLine(operationBody);
      return null;
    }

    Console.WriteLine("OperaÃ§Ã£o registrada.");
    Console.WriteLine(operationBody);

    Console.WriteLine();
    Console.WriteLine("Registro da operacao concluido.");
    Console.WriteLine($"CustomerId para faturamento futuro: {customer.Id}");
    Console.WriteLine();
    Console.WriteLine("Guarde esse CustomerId. Utilize a opcao 5 apos o fechamento do mes.");

    return customer.Id;
  }

  public static async Task<Guid?> GenerateAsync(
      VehicleBillingApiClient apiClient)
  {
    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine(" GERAR FATURA POS-PAGA");
    Console.WriteLine("======================================");
    Console.WriteLine();

    Console.Write("Informe o CustomerId: ");

    if (!Guid.TryParse(Console.ReadLine(), out var customerId) ||
        customerId == Guid.Empty)
    {
      Console.WriteLine("CustomerId invalido.");
      return null;
    }

    Console.Write("Informe o mes de referencia (yyyy-MM): ");

    var input = Console.ReadLine();

    if (!DateOnly.TryParseExact(
            input,
            "yyyy-MM",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var referenceDate))
    {
      Console.WriteLine("Mes de referencia invalido. Utilize yyyy-MM.");
      return null;
    }

    referenceDate = new DateOnly(
        referenceDate.Year,
        referenceDate.Month,
        1);

    var generationId = Guid.NewGuid();

    Console.WriteLine();
    Console.WriteLine($"CustomerId: {customerId}");
    Console.WriteLine($"Referencia: {referenceDate:yyyy-MM}");
    Console.WriteLine($"GenerationId: {generationId}");
    Console.WriteLine();
    Console.WriteLine("Gerando fatura...");

    using var invoiceResponse =
        await apiClient.CreateInvoiceAsync(
            customerId,
            referenceDate,
            generationId);

    var invoiceBody =
        await invoiceResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"HTTP fatura: {(int)invoiceResponse.StatusCode} " +
        invoiceResponse.StatusCode);

    if (!invoiceResponse.IsSuccessStatusCode)
    {
      Console.WriteLine("Nao foi possivel gerar a fatura.");
      Console.WriteLine(invoiceBody);
      return null;
    }

    var invoice =
        JsonSerializer.Deserialize<InvoiceResponse>(
            invoiceBody,
            JsonOptions);

    if (invoice is null || invoice.Id == Guid.Empty)
    {
      Console.WriteLine("Resposta invalida ao gerar a fatura.");
      Console.WriteLine(invoiceBody);
      return null;
    }

    Console.WriteLine();
    Console.WriteLine("FATURA GERADA");
    Console.WriteLine("--------------------------------------");
    Console.WriteLine($"ID:           {invoice.Id}");
    Console.WriteLine($"Cliente:      {invoice.CustomerId}");
    Console.WriteLine(
        $"Periodo:      {invoice.PeriodStart:yyyy-MM-dd} ate " +
        $"{invoice.PeriodEnd:yyyy-MM-dd}");
    Console.WriteLine($"Valor:        R$ {invoice.Amount:N2}");
    Console.WriteLine($"Status:       {invoice.Status}");
    Console.WriteLine(
        $"Criada em:    {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");

    Console.WriteLine();
    Console.WriteLine("Testando idempotencia da geracao...");

    var secondGenerationId = Guid.NewGuid();

    using var secondInvoiceResponse =
        await apiClient.CreateInvoiceAsync(
            customerId,
            referenceDate,
            secondGenerationId);

    var secondInvoiceBody =
        await secondInvoiceResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"Segunda geracao: {(int)secondInvoiceResponse.StatusCode} " +
        secondInvoiceResponse.StatusCode);

    if (!secondInvoiceResponse.IsSuccessStatusCode)
    {
      Console.WriteLine("A segunda tentativa de geracao falhou.");
      Console.WriteLine(secondInvoiceBody);
      return null;
    }

    var secondInvoice =
        JsonSerializer.Deserialize<InvoiceResponse>(
            secondInvoiceBody,
            JsonOptions);

    if (secondInvoice is null || secondInvoice.Id != invoice.Id)
    {
      Console.WriteLine(
          "FALHA DE IDEMPOTENCIA: a segunda tentativa nao retornou a mesma fatura.");

      Console.WriteLine(secondInvoiceBody);
      return null;
    }

    Console.WriteLine(
        "Idempotencia confirmada: a segunda tentativa retornou a mesma fatura.");

    Console.WriteLine();
    Console.WriteLine("FATURAMENTO CONCLUIDO COM SUCESSO");
    Console.WriteLine($"Guarde o ID da fatura: {invoice.Id}");

    return invoice.Id;
  }

  public static async Task GetAsync(
      VehicleBillingApiClient apiClient)
  {
    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine(" CONSULTAR FATURA");
    Console.WriteLine("======================================");
    Console.WriteLine();

    Console.Write("Informe o ID da fatura: ");

    if (!Guid.TryParse(
            Console.ReadLine(),
            out var invoiceId) ||
        invoiceId == Guid.Empty)
    {
      Console.WriteLine("ID da fatura invÃ¡lido.");
      return;
    }

    using var response =
        await apiClient.GetInvoiceByIdAsync(invoiceId);

    Console.WriteLine();
    Console.WriteLine(
        $"HTTP: {(int)response.StatusCode} {response.StatusCode}");

    if (response.StatusCode == HttpStatusCode.NotFound)
    {
      Console.WriteLine("Fatura nÃ£o encontrada.");
      return;
    }

    if (!response.IsSuccessStatusCode)
    {
      var error =
          await response.Content.ReadAsStringAsync();

      Console.WriteLine($"Resposta: {error}");
      return;
    }

    var invoice =
        await response.Content.ReadFromJsonAsync<InvoiceResponse>(
            JsonOptions);

    if (invoice is null)
    {
      Console.WriteLine("Resposta da API invÃ¡lida.");
      return;
    }

    Console.WriteLine();
    Console.WriteLine("FATURA");
    Console.WriteLine("--------------------------------------");
    Console.WriteLine($"ID:           {invoice.Id}");
    Console.WriteLine($"Cliente:      {invoice.CustomerId}");
    Console.WriteLine(
        $"PerÃ­odo:      {invoice.PeriodStart:yyyy-MM-dd} atÃ© " +
        $"{invoice.PeriodEnd:yyyy-MM-dd}");
    Console.WriteLine($"Valor:        R$ {invoice.Amount:N2}");
    Console.WriteLine($"Status:       {invoice.Status}");
    Console.WriteLine(
        $"Criada em:    {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");

    Console.WriteLine();
    Console.WriteLine("ITENS DA FATURA");
    Console.WriteLine("--------------------------------------");

    if (invoice.Items is null || invoice.Items.Count == 0)
    {
      Console.WriteLine("Nenhum item encontrado.");
    }
    else
    {
      foreach (var item in invoice.Items)
      {
        Console.WriteLine(
            $"OperaÃ§Ã£o: {item.OperationId} | " +
            $"Valor: R$ {item.Amount:N2}");
      }
    }

    Console.WriteLine("--------------------------------------");
    Console.WriteLine($"TOTAL: R$ {invoice.Amount:N2}");
  }
}

