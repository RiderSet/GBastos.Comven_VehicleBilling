namespace Comven_VehicleBilling.WorkerService.Events;

public sealed record InvoiceCreatedItemEvent(
    Guid OperationId,
    decimal Amount);

public sealed record InvoiceCreatedEvent(
    Guid EventId,
    Guid InvoiceId,
    Guid CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Total,
    DateTimeOffset CreatedAt,
    InvoiceCreatedItemEvent[]? Items);