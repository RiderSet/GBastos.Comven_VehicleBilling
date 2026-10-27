namespace Comven_VehicleBilling.Application.Operations;

public sealed record CreateOperationCommand(
    Guid ExternalOperationId,
    Guid CustomerId,
    string OperationType,
    decimal Amount);
