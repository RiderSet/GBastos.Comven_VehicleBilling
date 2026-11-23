namespace Comven_VehicleBilling.Application.Invoices;

public sealed record InvoiceItemDto(
    Guid OperationId,
    decimal Amount);

public sealed record InvoiceDto(
    Guid Id,
    Guid CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? OriginalInvoiceId,
    IReadOnlyList<InvoiceItemDto> Items)
{
  public bool IsSupplementary => OriginalInvoiceId.HasValue;
  public string? ExternalPaymentId { get; init; }
}
