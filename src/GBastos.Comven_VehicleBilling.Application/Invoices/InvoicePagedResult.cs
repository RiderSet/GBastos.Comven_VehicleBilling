namespace Comven_VehicleBilling.Application.Invoices;

public sealed record InvoicePagedResult(
    IReadOnlyList<InvoiceDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
  public int TotalPages =>
      (int)Math.Ceiling(TotalCount / (double)PageSize);

  public bool HasNextPage =>
      Page < TotalPages;

  public bool HasPreviousPage =>
      Page > 1 && TotalCount > 0;
}
