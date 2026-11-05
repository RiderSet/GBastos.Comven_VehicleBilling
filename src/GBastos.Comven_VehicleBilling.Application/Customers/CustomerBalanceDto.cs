namespace Comven_VehicleBilling.Application.Customers;

public sealed record CustomerBalanceDto(
    Guid CustomerId,
    decimal Balance,
    string Currency);
