namespace Comven_VehicleBilling.Application.Operations;

public sealed record OperationPagedResult(
    IReadOnlyList<OperationDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
  public int TotalPages =>
      TotalCount == 0
          ? 0
          : (int)Math.Ceiling((double)TotalCount / PageSize);

  public bool HasNextPage => Page < TotalPages;

  public bool HasPreviousPage => Page > 1;
}
