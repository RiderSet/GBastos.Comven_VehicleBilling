namespace Comven_VehicleBilling.Application.Operations;

public sealed record OperationDto(
    Guid Id,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    DateTime CreatedAt);
