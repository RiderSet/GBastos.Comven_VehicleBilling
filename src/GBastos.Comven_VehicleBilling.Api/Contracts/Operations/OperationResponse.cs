namespace Comven_VehicleBilling.Api.Contracts.Operations;

public sealed record OperationResponse(
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
