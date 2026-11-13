namespace Comven_VehicleBilling.Application.Invoices;

public sealed record CreateSupplementaryInvoiceCommand(
    Guid CustomerId,
    Guid OriginalInvoiceId,
    string IdempotencyKey);