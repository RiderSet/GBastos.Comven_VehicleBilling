namespace Comven_VehicleBilling.Application.Statements;

public sealed record StatementDto(
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance);
