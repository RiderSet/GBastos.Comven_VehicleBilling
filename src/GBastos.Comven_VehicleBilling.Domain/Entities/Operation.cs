using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class Operation
{
  public Guid Id { get; private set; }
  public Guid ExternalOperationId { get; private set; }
  public Guid CustomerId { get; private set; }
  public OperationType Type { get; private set; }
  public Money Amount { get; private set; } = Money.Zero;
  public OperationStatus Status { get; private set; }
  public DateTimeOffset CreatedAt { get; private set; }
  public DateTimeOffset? RefundedAt { get; private set; }
  public DateTimeOffset? InvoicedAt { get; private set; }
  public Guid? InvoiceId { get; private set; }
  public Customer Customer { get; private set; } = null!;
  public Invoice? Invoice { get; private set; }
  public string RequestHash { get; private set; } = string.Empty;
}
