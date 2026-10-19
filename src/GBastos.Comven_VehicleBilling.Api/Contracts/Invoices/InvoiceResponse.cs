namespace Comven_VehicleBilling.Api.Contracts.Invoices;

public sealed record InvoiceResponse(
    Guid Id,
    Guid CustomerId,
    DateOnly ReferenceDate,
    decimal Amount,
    string Status,
    DateTime CreatedAt);
