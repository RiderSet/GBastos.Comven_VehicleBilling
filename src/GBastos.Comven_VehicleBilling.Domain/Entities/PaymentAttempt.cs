namespace Comven_VehicleBilling.Domain.Entities;

public class PaymentAttempt
{
  public Guid Id { get; private set; }
  public Guid InvoiceId { get; private set; }
  public decimal Amount { get; private set; }
  public PaymentStatus Status { get; private set; }
  public string? ErrorMessage { get; private set; }
  public DateTime AttemptedAt { get; private set; }

  // Construtor protegido/privado para o EF Core
  private PaymentAttempt() { }

  public PaymentAttempt(Guid invoiceId, decimal amount, PaymentStatus status, string? errorMessage = null)
  {
    Id = Guid.NewGuid();
    InvoiceId = invoiceId;
    Amount = amount > 0 ? amount : throw new ArgumentException("O valor da tentativa deve ser maior que zero.", nameof(amount));
    Status = status;
    ErrorMessage = errorMessage;
    AttemptedAt = DateTime.UtcNow;
  }

  public void MarkAsSuccess()
  {
    Status = PaymentStatus.Success;
    ErrorMessage = null;
  }

  public void MarkAsFailed(string errorMessage)
  {
    Status = PaymentStatus.Failed;
    ErrorMessage = errorMessage;
  }
}

public enum PaymentStatus
{
  Pending,
  Success,
  Failed
}
