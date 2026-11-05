namespace Comven_VehicleBilling.Application.Operations;

public sealed record OperationDto(
    Guid Id,
    Guid ExternalOperationId,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RefundedAt,
    DateTimeOffset? InvoicedAt,
    Guid? InvoiceId);
