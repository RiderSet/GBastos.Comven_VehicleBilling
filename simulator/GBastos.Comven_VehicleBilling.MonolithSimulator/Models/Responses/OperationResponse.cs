namespace Comven_VehicleBilling.MonolithSimulator.Models.Responses;

public sealed record OperationResponse(
    Guid Id,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    DateTime CreatedAt);
