namespace Comven_VehicleBilling.Api.Contracts.Operations;

public sealed record CreateOperationRequest(
    Guid CustomerId,
    string OperationType,
    decimal Amount);
