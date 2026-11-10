using System.Text.Json;

namespace Comven_VehicleBilling.WorkerService.Events;

public sealed class OperationRefundedHandler
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  private readonly ILogger<OperationRefundedHandler> _logger;

  public OperationRefundedHandler(
      ILogger<OperationRefundedHandler> logger)
  {
    _logger = logger;
  }

  public Task HandleAsync(
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

    // O estorno financeiro já foi efetivado pela API.
    // Este handler não deve creditar saldo nem alterar a operação.
    // A Inbox garante que a mensagem não seja processada novamente.

    _logger.LogInformation(
        "Evento OperationRefunded.v1 processado. " +
        "EventId: {EventId}, OperationId: {OperationId}, " +
        "CustomerId: {CustomerId}, Amount: {Amount}",
        message.EventId,
        message.OperationId,
        message.CustomerId,
        message.Amount);

    return Task.CompletedTask;
  }
}
