namespace Comven_VehicleBilling.WorkerService.Events;

public sealed record OperationRefundedEvent(
    Guid EventId,
    Guid OperationId,
    string ExternalOperationId,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    DateTime RefundedAt);
