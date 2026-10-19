namespace Comven_VehicleBilling.Api.Contracts.Statements;

public sealed record GetStatementRequest(
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate);
