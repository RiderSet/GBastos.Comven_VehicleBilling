namespace Comven_VehicleBilling.Application.Invoices;

public sealed record CreateInvoiceCommand(
    Guid CustomerId,
    DateOnly ReferenceDate);
