namespace Comven_VehicleBilling.Application.Invoices;

public sealed record InvoiceDto(
    Guid Id,
    Guid CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? OriginalInvoiceId)
{
  public bool IsSupplementary => OriginalInvoiceId.HasValue;
}
