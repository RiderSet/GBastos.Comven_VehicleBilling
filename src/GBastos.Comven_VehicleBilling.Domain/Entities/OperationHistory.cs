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
    Id = Guid.NewGuid();
    OperationId = operationId;
    Type = type;
    Message = message;
    ProviderName = providerName;
    CreatedAt = DateTimeOffset.UtcNow;
  }
}
