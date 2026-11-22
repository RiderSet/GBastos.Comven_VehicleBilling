namespace Comven_VehicleBilling.Application.Abstractions.Payments;

public interface IPaymentProvider
{
    Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);
}
