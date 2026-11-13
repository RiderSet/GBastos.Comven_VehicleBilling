namespace Comven_VehicleBilling.Api.Contracts.Invoices;

public sealed record CreateSupplementaryInvoiceRequest(
    Guid CustomerId,
    Guid OriginalInvoiceId,
    string IdempotencyKey);