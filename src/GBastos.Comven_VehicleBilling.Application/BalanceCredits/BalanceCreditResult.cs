namespace Comven_VehicleBilling.Application.BalanceCredits;

public sealed record BalanceCreditResult(
    Guid CreditId,
    Guid CustomerId,
    decimal Amount,
    decimal Balance,
    DateTimeOffset CreatedAt,
    bool AlreadyProcessed);
