using Comven_VehicleBilling.Application.BalanceCredits;

namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface IBalanceCreditRepository
{
    Task<BalanceCreditResult> CreditAsync(
        Guid customerId,
        string externalReference,
        decimal amount,
        CancellationToken cancellationToken = default);
}
