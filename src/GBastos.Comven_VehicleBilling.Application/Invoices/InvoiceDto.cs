namespace Comven_VehicleBilling.Application.Invoices;

public sealed record InvoiceDto(
    Guid Id,
    Guid CustomerId,
    DateOnly ReferenceDate,
    decimal Amount,
    string Status,
    DateTime CreatedAt);
