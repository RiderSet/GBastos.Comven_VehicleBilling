namespace Comven_VehicleBilling.Api.Contracts.Operations;

public sealed record OperationResponse(
    Guid Id,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    DateTime CreatedAt);
