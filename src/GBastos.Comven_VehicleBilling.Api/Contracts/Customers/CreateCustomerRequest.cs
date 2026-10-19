namespace Comven_VehicleBilling.Api.Contracts.Customers;

public sealed record CreateCustomerRequest(
    string Name,
    string Document,
    string Email);
