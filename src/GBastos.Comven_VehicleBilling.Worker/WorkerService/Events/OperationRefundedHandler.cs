using System.Text.Json;
using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.WorkerService.Events;

public sealed class OperationRefundedHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<OperationRefundedHandler> _logger;
    private readonly IOperationRepository _operationRepository;

    public OperationRefundedHandler(
        ILogger<OperationRefundedHandler> logger,
        IOperationRepository operationRepository)
    {
        _logger = logger;
        _operationRepository = operationRepository;
    }

    public async Task HandleAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OperationRefundedEvent? message;

        try
        {
            message = JsonSerializer.Deserialize<OperationRefundedEvent>(
                body.Span,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new PermanentMessageException(
                "O payload de OperationRefunded.v1 não contém JSON válido.",
                exception);
        }

        if (message is null ||
            message.EventId == Guid.Empty ||
            message.OperationId == Guid.Empty ||
            message.CustomerId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.ExternalOperationId) ||
            string.IsNullOrWhiteSpace(message.OperationType) ||
            message.Amount <= 0 ||
            message.RefundedAt == default)
        {
            throw new PermanentMessageException(
                "O payload de OperationRefunded.v1 contém campos obrigatórios inválidos.");
        }

        // Consulta o estado persistido sem modificar dados financeiros.
        var operation = await _operationRepository.GetByIdAsync(
            message.OperationId,
            cancellationToken);

        if (operation is null)
        {
            throw new PermanentMessageException(
                $"A operação {message.OperationId} do evento de estorno não foi encontrada.");
        }

        // O evento representa um estorno já efetivado pela API.
        // O Worker apenas confirma a consistência do estado persistido.
        var refundedAtUtc = DateTime.SpecifyKind(
            message.RefundedAt,
            DateTimeKind.Utc);

        var isConsistent =
            operation.Id == message.OperationId &&
            operation.CustomerId == message.CustomerId &&
            string.Equals(
                operation.OperationType,
                message.OperationType,
                StringComparison.OrdinalIgnoreCase) &&
            operation.Amount == message.Amount &&
            string.Equals(
                operation.Status,
                "Refunded",
                StringComparison.OrdinalIgnoreCase) &&
            operation.RefundedAt.HasValue &&
            operation.RefundedAt.Value.UtcDateTime == refundedAtUtc;

        if (!isConsistent)
        {
            throw new PermanentMessageException(
                $"O estado persistido da operação {message.OperationId} diverge do evento OperationRefunded.v1.");
        }

        _logger.LogInformation(
            "Evento {EventId} de estorno confirmado. " +
            "OperationId={OperationId}, CustomerId={CustomerId}, Amount={Amount}.",
            message.EventId,
            message.OperationId,
            message.CustomerId,
            message.Amount);
    }
}
