using System.Text.Json;

namespace Comven_VehicleBilling.WorkerService.Events;

public sealed class SupplementaryInvoiceCreatedHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<SupplementaryInvoiceCreatedHandler> _logger;

    public SupplementaryInvoiceCreatedHandler(
        ILogger<SupplementaryInvoiceCreatedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SupplementaryInvoiceCreatedEvent? message;

        try
        {
            message = JsonSerializer.Deserialize<SupplementaryInvoiceCreatedEvent>(
                body.Span,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new PermanentMessageException(
                "O payload de SupplementaryInvoiceCreated.v1 não contém JSON válido.",
                exception);
        }

        if (message is null ||
            message.EventId == Guid.Empty ||
            message.InvoiceId == Guid.Empty ||
            message.OriginalInvoiceId == Guid.Empty ||
            message.InvoiceId == message.OriginalInvoiceId ||
            string.IsNullOrWhiteSpace(message.IdempotencyKey) ||
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
                "O payload de SupplementaryInvoiceCreated.v1 contém campos obrigatórios inválidos.");
        }

        // A fatura complementar já foi persistida pela API.
        // Não recriamos a fatura nem repetimos efeitos financeiros.
        // A Inbox registra o consumo idempotente deste evento.
        _logger.LogInformation(
            "Evento {EventId} de fatura complementar processado. " +
            "InvoiceId={InvoiceId}, OriginalInvoiceId={OriginalInvoiceId}, " +
            "CustomerId={CustomerId}, Total={Total}, ItemCount={ItemCount}.",
            message.EventId,
            message.InvoiceId,
            message.OriginalInvoiceId,
            message.CustomerId,
            message.Total,
            message.Items.Length);

        return Task.CompletedTask;
    }
}