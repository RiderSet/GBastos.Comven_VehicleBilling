namespace Comven_VehicleBilling.Api.Contracts.Operations;

public sealed record OperationLedgerEntryResponse(
    Guid Id,
    string Type,
    decimal Amount,
    string Description,
    DateTimeOffset CreatedAt);
