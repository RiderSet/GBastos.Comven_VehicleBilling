namespace Comven_VehicleBilling.Application.Statements;

public sealed record StatementEntryDto(
    Guid Id,
    Guid? OperationId,
    Guid? BalanceCreditId,
    decimal Amount,
    string Type,
    string Description,
    DateTimeOffset CreatedAt
);
