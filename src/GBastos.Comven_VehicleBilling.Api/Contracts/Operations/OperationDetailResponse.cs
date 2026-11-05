namespace Comven_VehicleBilling.Api.Contracts.Operations;

public sealed record OperationDetailResponse(
    Guid Id,
    Guid CustomerId,
    string OperationType,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? InvoicedAt,
    DateTimeOffset? RefundedAt,
    Guid? InvoiceId,
    IReadOnlyList<OperationLedgerEntryResponse> LedgerEntries);
