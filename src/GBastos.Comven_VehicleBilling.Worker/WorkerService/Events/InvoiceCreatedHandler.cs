using System.Text.Json;

namespace Comven_VehicleBilling.WorkerService.Events;

public sealed class InvoiceCreatedHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<InvoiceCreatedHandler> _logger;

    public InvoiceCreatedHandler(ILogger<InvoiceCreatedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
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

        // A fatura já foi persistida pela API.
        // O Worker não cria faturas nem repete efeitos financeiros.
        // A Inbox registra o consumo idempotente deste evento.
        _logger.LogInformation(
            "Evento {EventId} de fatura processado. " +
            "InvoiceId={InvoiceId}, CustomerId={CustomerId}, " +
            "Total={Total}, ItemCount={ItemCount}.",
            message.EventId,
            message.InvoiceId,
            message.CustomerId,
            message.Total,
            message.Items.Length);

        return Task.CompletedTask;
    }
}