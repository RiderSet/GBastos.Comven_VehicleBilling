namespace Comven_VehicleBilling.WorkerService.Events;

public sealed record SupplementaryInvoiceCreatedItemEvent(
    Guid OperationId,
    decimal Amount);

public sealed record SupplementaryInvoiceCreatedEvent(
    Guid EventId,
    Guid InvoiceId,
    Guid OriginalInvoiceId,
    string IdempotencyKey,
    Guid CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Total,
    DateTimeOffset CreatedAt,
    SupplementaryInvoiceCreatedItemEvent[]? Items);