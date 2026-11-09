using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Application.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string Document,
    string Email,
    CustomerPlan Plan = CustomerPlan.Prepaid);
