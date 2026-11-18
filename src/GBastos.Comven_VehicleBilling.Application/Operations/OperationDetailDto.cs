namespace Comven_VehicleBilling.Application.Operations;

public sealed record OperationDetailDto(
    Guid Id,
    Guid ExternalOperationId,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? InvoicedAt,
    DateTimeOffset? RefundedAt,
    Guid? InvoiceId,
    IReadOnlyList<OperationLedgerEntryDto> LedgerEntries);