namespace Comven_VehicleBilling.Application.Customers;

public sealed record CreateCustomerCommand(
    string Name,
    string Document,
    string Email);
