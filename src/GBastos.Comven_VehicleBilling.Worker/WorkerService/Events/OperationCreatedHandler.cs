using System.Text.Json;

namespace Comven_VehicleBilling.WorkerService.Events;

public sealed class OperationCreatedHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<OperationCreatedHandler> _logger;

    public OperationCreatedHandler(ILogger<OperationCreatedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OperationCreatedEvent? message;
        try
        {
            message = JsonSerializer.Deserialize<OperationCreatedEvent>(
                body.Span, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new PermanentMessageException(
                "O payload de OperationCreated.v1 não contém JSON válido.",
                exception);
        }

        if (message is null ||
            message.EventId == Guid.Empty ||
            message.OperationId == Guid.Empty ||
            message.CustomerId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.ExternalOperationId) ||
            string.IsNullOrWhiteSpace(message.OperationType) ||
            string.IsNullOrWhiteSpace(message.CustomerPlan) ||
            message.Amount <= 0 ||
            message.CreatedAt == default)
        {
            throw new PermanentMessageException(
                "O payload de OperationCreated.v1 contém campos obrigatórios inválidos.");
        }

        // O evento atualmente representa a notificação da operação já persistida
        // pela API. A Inbox é o registro durável de consumo; não repetimos aqui
        // a cobrança nem qualquer outro efeito financeiro.
        _logger.LogInformation(
            "Evento {EventId} aceito. OperationId={OperationId}, " +
            "CustomerId={CustomerId}, OperationType={OperationType}.",
            message.EventId,
            message.OperationId,
            message.CustomerId,
            message.OperationType);

        return Task.CompletedTask;
    }
}
