using Comven_VehicleBilling.Domain.ValueObjects;

public sealed class AccountTransaction
{
  public Guid Id { get; private set; }
  public Guid CustomerId { get; private set; }
  public Guid OperationId { get; private set; }
  public TransactionType Type { get; private set; }
  public Money Amount { get; private set; } = Money.Zero;
  public DateTimeOffset CreatedAt { get; private set; }
}
