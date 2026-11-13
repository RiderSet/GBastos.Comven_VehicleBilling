using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Comven_VehicleBilling.MonolithSimulator.Clients;
using Comven_VehicleBilling.MonolithSimulator.Models.Requests;
using Comven_VehicleBilling.MonolithSimulator.Models.Responses;

namespace Comven_VehicleBilling.MonolithSimulator.Scenarios;

internal static class InvoiceScenarios
{
  public static async Task CreateAsync(
      VehicleBillingApiClient apiClient)
  {
    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine(" CENÁRIO: FATURAMENTO PÓS-PAGO");
    Console.WriteLine("======================================");
    Console.WriteLine();

    Console.Write("Informe a data de referência (yyyy-MM-dd): ");

    if (!DateOnly.TryParse(
            Console.ReadLine(),
            out var referenceDate))
    {
      Console.WriteLine("Data de referência inválida.");
      return;
    }

    var email =
        $"simulador-postpaid-{Guid.NewGuid():N}@comven.com.br";

    var document =
        $"SIM{Guid.NewGuid():N}"[..14];

    Console.WriteLine();
    Console.WriteLine("Criando cliente pós-pago...");

    using var customerResponse =
        await apiClient.CreateCustomerAsync(
            "Cliente Simulador Pós-Pago",
            document,
            email,
            plan: "Postpaid");

    var customerBody =
        await customerResponse.Content.ReadAsStringAsync();

    if (!customerResponse.IsSuccessStatusCode)
    {
      Console.WriteLine("Não foi possível criar o cliente.");
      Console.WriteLine(customerBody);
      return;
    }

    var customer =
        JsonSerializer.Deserialize<CustomerResponse>(
            customerBody,
            new JsonSerializerOptions
            {
              PropertyNameCaseInsensitive = true
            });

    if (customer is null || customer.Id == Guid.Empty)
    {
      Console.WriteLine("Resposta inválida ao criar cliente.");
      Console.WriteLine(customerBody);
      return;
    }

    Console.WriteLine($"Cliente criado: {customer.Id}");
    Console.WriteLine("Plano: Postpaid");

    Console.WriteLine();
    Console.WriteLine("Registrando operação pós-paga...");

    var operationRequest = new CreateOperationRequest(
        Guid.NewGuid(),
        customer.Id,
        "VehicleConsultation",
        25.00m);

    using var operationResponse =
        await apiClient.CreateOperationAsync(operationRequest);

    var operationBody =
        await operationResponse.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"HTTP operação: {(int)operationResponse.StatusCode} " +
        operationResponse.StatusCode);

    if (!operationResponse.IsSuccessStatusCode)
    {
      Console.WriteLine("Não foi possível registrar a operação.");
      Console.WriteLine(operationBody);
      return;
    }

    Console.WriteLine("Operação registrada.");
    Console.WriteLine(operationBody);

    Console.WriteLine();
    Console.WriteLine("Gerando fatura...");

    var generationId = Guid.NewGuid();

    Console.WriteLine($"GenerationId: {generationId}");

    using var invoiceResponse =
        await apiClient.CreateInvoiceAsync(
            customer.Id,
            referenceDate,
            generationId);

    var invoiceBody =
        await invoiceResponse.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine(
        $"HTTP fatura: {(int)invoiceResponse.StatusCode} " +
        invoiceResponse.StatusCode);

    if (!invoiceResponse.IsSuccessStatusCode)
    {
      Console.WriteLine("Não foi possível gerar a fatura.");
      Console.WriteLine(invoiceBody);
      return;
    }

    var invoice =
        JsonSerializer.Deserialize<InvoiceResponse>(
            invoiceBody,
            new JsonSerializerOptions
            {
              PropertyNameCaseInsensitive = true
            });

    if (invoice is null)
    {
      Console.WriteLine("Resposta da API não contém uma fatura válida.");
      Console.WriteLine(invoiceBody);
      return;
    }

    Console.WriteLine();
    Console.WriteLine("FATURA GERADA");
    Console.WriteLine("--------------------------------------");
    Console.WriteLine($"ID:           {invoice.Id}");
    Console.WriteLine($"Cliente:      {invoice.CustomerId}");
    Console.WriteLine(
        $"Período:      {invoice.PeriodStart:yyyy-MM-dd} até " +
        $"{invoice.PeriodEnd:yyyy-MM-dd}");
    Console.WriteLine($"Valor:        {invoice.Amount:N2}");
    Console.WriteLine($"Status:       {invoice.Status}");
    Console.WriteLine(
        $"Criada em:    {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");
    Console.WriteLine();
    Console.WriteLine("Guarde o ID da fatura para o cenário 4.");
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

    if (!Guid.TryParse(Console.ReadLine(), out var invoiceId) ||
        invoiceId == Guid.Empty)
    {
      Console.WriteLine("ID da fatura inválido.");
      return;
    }

    using var response =
        await apiClient.GetInvoiceByIdAsync(invoiceId);

    Console.WriteLine();
    Console.WriteLine(
        $"HTTP: {(int)response.StatusCode} {response.StatusCode}");

    if (response.StatusCode == HttpStatusCode.NotFound)
    {
      Console.WriteLine("Fatura não encontrada.");
      return;
    }

    if (!response.IsSuccessStatusCode)
    {
      var error = await response.Content.ReadAsStringAsync();
      Console.WriteLine($"Resposta: {error}");
      return;
    }

    var invoice =
        await response.Content.ReadFromJsonAsync<InvoiceResponse>();

    if (invoice is null)
    {
      Console.WriteLine("Resposta da API inválida.");
      return;
    }

    Console.WriteLine();
    Console.WriteLine("FATURA");
    Console.WriteLine("--------------------------------------");
    Console.WriteLine($"ID:           {invoice.Id}");
    Console.WriteLine($"Cliente:      {invoice.CustomerId}");
    Console.WriteLine(
        $"Período:      {invoice.PeriodStart:yyyy-MM-dd} até " +
        $"{invoice.PeriodEnd:yyyy-MM-dd}");
    Console.WriteLine($"Valor:        {invoice.Amount:N2}");
    Console.WriteLine($"Status:       {invoice.Status}");
    Console.WriteLine(
        $"Criada em:    {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");
  }
}
