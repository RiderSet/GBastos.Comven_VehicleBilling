namespace Comven_VehicleBilling.MonolithSimulator.Models.Requests;

public sealed record CreateOperationRequest(
    Guid ExternalOperationId,
    Guid CustomerId,
    string OperationType,
    decimal Amount);
