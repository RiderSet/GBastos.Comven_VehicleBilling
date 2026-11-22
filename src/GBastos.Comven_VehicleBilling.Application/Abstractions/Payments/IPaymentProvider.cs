namespace Comven_VehicleBilling.Application.Abstractions.Payments;

public interface IPaymentProvider
{
  Task<PaymentResult> ProcessPaymentAsync(
      PaymentRequest request,
      CancellationToken cancellationToken = default);

  Task<PaymentResult?> GetPaymentAsync(
      string idempotencyKey,
      CancellationToken cancellationToken = default);
}
