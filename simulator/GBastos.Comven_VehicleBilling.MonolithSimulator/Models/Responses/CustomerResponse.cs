namespace Comven_VehicleBilling.MonolithSimulator.Models.Responses;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Document,
    string Email);
