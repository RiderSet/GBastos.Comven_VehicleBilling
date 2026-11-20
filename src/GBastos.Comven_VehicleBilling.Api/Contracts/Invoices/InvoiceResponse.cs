namespace Comven_VehicleBilling.Api.Contracts.Invoices;

public sealed record InvoiceItemResponse(
    Guid OperationId,
    decimal Amount);

public sealed record InvoiceResponse(
    Guid Id,
    Guid CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? OriginalInvoiceId = null,
    bool IsSupplementary = false,
    IReadOnlyList<InvoiceItemResponse>? Items = null);

