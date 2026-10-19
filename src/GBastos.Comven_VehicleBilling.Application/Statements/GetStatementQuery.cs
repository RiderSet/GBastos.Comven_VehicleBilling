namespace Comven_VehicleBilling.Application.Statements;

public sealed record GetStatementQuery(
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate);
