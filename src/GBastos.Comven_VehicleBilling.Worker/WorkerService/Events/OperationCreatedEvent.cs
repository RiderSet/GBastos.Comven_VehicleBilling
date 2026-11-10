namespace Comven_VehicleBilling.WorkerService.Events;

public sealed record OperationCreatedEvent(
    Guid EventId,
    Guid OperationId,
    string ExternalOperationId,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    string CustomerPlan,
    DateTime CreatedAt);
