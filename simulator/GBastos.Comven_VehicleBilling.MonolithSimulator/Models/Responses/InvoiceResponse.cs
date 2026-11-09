namespace Comven_VehicleBilling.MonolithSimulator.Models.Responses;

public sealed record InvoiceResponse(
    Guid Id,
    Guid CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt);
