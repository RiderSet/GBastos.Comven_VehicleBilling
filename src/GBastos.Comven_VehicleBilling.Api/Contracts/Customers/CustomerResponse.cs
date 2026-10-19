namespace Comven_VehicleBilling.Api.Contracts.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Document,
    string Email);
