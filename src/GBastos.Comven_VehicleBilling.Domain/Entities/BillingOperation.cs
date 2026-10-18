using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class BillingOperation
{
  public Guid Id { get; private set; }
  public Guid CustomerId { get; private set; }
  public OperationType Type { get; private set; }
  public string Provider { get; private set; } = string.Empty;
  public decimal Amount { get; private set; }
  public OperationStatus Status { get; private set; }
  public DateTimeOffset CreatedAt { get; private set; }
  public DateTimeOffset? CompletedAt { get; private set; }
  public Guid? InvoiceId { get; private set; }

  private BillingOperation() { }

  public BillingOperation(
      Guid id,
      Guid customerId,
      OperationType type,
      decimal amount,
      OperationStatus status,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty || customerId == Guid.Empty)
      throw new ArgumentException("IDs are required.");

    if (amount <= 0)
      throw new ArgumentOutOfRangeException(nameof(amount));

    Id = id;
    CustomerId = customerId;
    Type = type;
    Amount = amount;
    Status = status;
    CreatedAt = createdAt;
  }

  public void Complete(string provider, DateTimeOffset completedAt)
  {
    if (Status is not OperationStatus.Accepted
        and not OperationStatus.PendingInvoice)
      throw new InvalidOperationException(
          "Operation cannot be completed from its current state.");

    Provider = provider;
    CompletedAt = completedAt;

    if (Status == OperationStatus.Accepted)
      Status = OperationStatus.Succeeded;
  }

  public void MarkFailed()
  {
    if (Status is not OperationStatus.Accepted
        and not OperationStatus.PendingInvoice)
      throw new InvalidOperationException(
          "Operation cannot be failed from its current state.");

    Status = OperationStatus.Failed;
  }

  public void MarkRefunded()
  {
    if (Status != OperationStatus.Succeeded)
      throw new InvalidOperationException(
          "Only a successful prepaid operation can be refunded.");

    Status = OperationStatus.Refunded;
  }

  public void MarkInvoiced(Guid invoiceId)
  {
    if (Status != OperationStatus.PendingInvoice)
      throw new InvalidOperationException(
          "Only pending operations can be invoiced.");

    if (invoiceId == Guid.Empty)
      throw new ArgumentException("Invoice ID is required.");

    InvoiceId = invoiceId;
    Status = OperationStatus.Invoiced;
  }
}
