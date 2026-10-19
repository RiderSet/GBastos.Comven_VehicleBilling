namespace Comven_VehicleBilling.Api.Contracts.Customers;

public sealed record CreateOperationRequest(
    Guid CustomerId,
    string OperationType,
    decimal Amount);
