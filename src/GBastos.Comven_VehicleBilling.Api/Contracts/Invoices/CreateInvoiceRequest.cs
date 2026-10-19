namespace Comven_VehicleBilling.Api.Contracts.Invoices;

public sealed record CreateInvoiceRequest(
    Guid CustomerId,
    DateOnly ReferenceDate);
