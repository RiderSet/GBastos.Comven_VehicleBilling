namespace Comven_VehicleBilling.Api.Contracts.Operations;

public sealed record PagedOperationResponse(
    IReadOnlyList<OperationResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
