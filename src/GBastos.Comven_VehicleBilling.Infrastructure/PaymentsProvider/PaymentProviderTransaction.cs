using Comven_VehicleBilling.Application.Abstractions.Payments;

namespace Comven_VehicleBilling.Infrastructure.PaymentsProvider;

public sealed class PaymentProviderTransaction
{
  public Guid Id { get; set; }

  public string IdempotencyKey { get; set; } = string.Empty;

  public Guid InvoiceId { get; set; }

  public Guid CustomerId { get; set; }

  public decimal Amount { get; set; }

  public PaymentOutcome Outcome { get; set; }

  public string? ExternalPaymentId { get; set; }

  public string? ErrorMessage { get; set; }

  public DateTime CreatedAt { get; set; }
}
