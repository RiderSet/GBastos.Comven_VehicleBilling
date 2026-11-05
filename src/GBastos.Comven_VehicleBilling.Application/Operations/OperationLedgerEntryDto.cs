namespace Comven_VehicleBilling.Application.Operations;

public sealed record OperationLedgerEntryDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Description,
    DateTimeOffset CreatedAt);
