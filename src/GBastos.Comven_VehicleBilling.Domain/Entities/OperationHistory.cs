namespace Comven_VehicleBilling.Domain.Entities;

public sealed class OperationHistory
{
  public Guid Id { get; private set; }
  public Guid OperationId { get; private set; }
  public OperationHistoryType Type { get; private set; }
  public string Message { get; private set; } = string.Empty;
  public string? ProviderName { get; private set; }
  public DateTimeOffset CreatedAt { get; private set; }
  public Operation Operation { get; private set; } = null!;

  private OperationHistory()
  {
  }

  public OperationHistory(
      Guid operationId,
      OperationHistoryType type,
      string message,
      string? providerName = null)
  {
    if (operationId == Guid.Empty)
      throw new ArgumentException(
          "O ID da operação é obrigatório.", nameof(operationId));

    if (string.IsNullOrWhiteSpace(message))
      throw new ArgumentException(
          "A mensagem do histórico é obrigatória.", nameof(message));

    Id = Guid.NewGuid();
    OperationId = operationId;
    Type = type;
    Message = message.Trim();
    ProviderName = string.IsNullOrWhiteSpace(providerName)
        ? null
        : providerName.Trim();
    CreatedAt = DateTimeOffset.UtcNow;
  }
}
