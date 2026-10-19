namespace Comven_VehicleBilling.Api.Contracts.Statements;

public sealed record StatementResponse(
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance);
