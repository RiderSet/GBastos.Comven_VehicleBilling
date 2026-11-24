using System.Text.Json;
using Comven_VehicleBilling.Application.Invoices;

namespace Comven_VehicleBilling.WorkerService.Events;

public sealed class InvoiceCreatedHandler
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  private readonly ILogger<InvoiceCreatedHandler> _logger;
  private readonly InvoicePaymentService _invoicePaymentService;

  public InvoiceCreatedHandler(
      ILogger<InvoiceCreatedHandler> logger,
      InvoicePaymentService invoicePaymentService)
  {
    _logger = logger;
    _invoicePaymentService = invoicePaymentService;
  }

  public async Task HandleAsync(
      ReadOnlyMemory<byte> body,
      CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    InvoiceCreatedEvent? message;

    try
    {
      message = JsonSerializer.Deserialize<InvoiceCreatedEvent>(
          body.Span,
          JsonOptions);
    }
    catch (JsonException exception)
    {
      throw new PermanentMessageException(
          "O payload de InvoiceCreated.v1 não contém JSON válido.",
          exception);
    }

    if (message is null ||
        message.EventId == Guid.Empty ||
        message.InvoiceId == Guid.Empty ||
        message.CustomerId == Guid.Empty ||
        message.PeriodStart == default ||
        message.PeriodEnd < message.PeriodStart ||
        message.Total <= 0 ||
        message.CreatedAt == default ||
        message.Items is null ||
        message.Items.Length == 0 ||
        message.Items.Any(item =>
            item.OperationId == Guid.Empty ||
            item.Amount <= 0))
    {
      throw new PermanentMessageException(
          "O payload de InvoiceCreated.v1 contém campos obrigatórios inválidos.");
    }

    _logger.LogInformation(
        "Iniciando processamento de pagamento. " +
        "EventId={EventId}, InvoiceId={InvoiceId}, Total={Total}.",
        message.EventId,
        message.InvoiceId,
        message.Total);

    // O serviço utiliza a chave de idempotência da própria fatura.
    // Em caso de resultado desconhecido, realiza reconciliação.
    var invoice = await _invoicePaymentService.PayAsync(
        message.InvoiceId,
        cancellationToken);

    _logger.LogInformation(
        "Processamento da fatura concluído. " +
        "EventId={EventId}, InvoiceId={InvoiceId}, Status={Status}.",
        message.EventId,
        invoice.Id,
        invoice.Status);
  }
}
