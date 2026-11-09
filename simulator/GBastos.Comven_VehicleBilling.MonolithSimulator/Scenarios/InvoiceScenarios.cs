using System.Net;
using System.Net.Http.Json;
using Comven_VehicleBilling.MonolithSimulator.Clients;
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

        Console.Write("Informe o ID do cliente: ");

        if (!Guid.TryParse(Console.ReadLine(), out var customerId) ||
            customerId == Guid.Empty)
        {
            Console.WriteLine("ID do cliente inválido.");
            return;
        }

        Console.Write("Informe a data de referência (yyyy-MM-dd): ");

        if (!DateOnly.TryParse(Console.ReadLine(), out var referenceDate))
        {
            Console.WriteLine("Data de referência inválida.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Gerando fatura...");

        using var response = await apiClient.CreateInvoiceAsync(
            customerId,
            referenceDate);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine();
        Console.WriteLine(
            $"HTTP: {(int)response.StatusCode} {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Não foi possível gerar a fatura.");
            Console.WriteLine($"Resposta: {body}");
            return;
        }

        var invoice =
            System.Text.Json.JsonSerializer.Deserialize<InvoiceResponse>(
                body,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (invoice is null)
        {
            Console.WriteLine("Resposta da API não contém uma fatura válida.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("FATURA GERADA");
        Console.WriteLine("--------------------------------------");
        Console.WriteLine($"ID:           {invoice.Id}");
        Console.WriteLine($"Cliente:      {invoice.CustomerId}");
        Console.WriteLine($"Período:      {invoice.PeriodStart:yyyy-MM-dd} até {invoice.PeriodEnd:yyyy-MM-dd}");
        Console.WriteLine($"Valor:        {invoice.Amount:N2}");
        Console.WriteLine($"Status:       {invoice.Status}");
        Console.WriteLine($"Criada em:    {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");
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
        Console.WriteLine($"Período:      {invoice.PeriodStart:yyyy-MM-dd} até {invoice.PeriodEnd:yyyy-MM-dd}");
        Console.WriteLine($"Valor:        {invoice.Amount:N2}");
        Console.WriteLine($"Status:       {invoice.Status}");
        Console.WriteLine($"Criada em:    {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");
    }
}
