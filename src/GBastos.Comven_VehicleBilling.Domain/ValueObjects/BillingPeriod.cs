namespace Comven_VehicleBilling.Domain.ValueObjects;

public sealed record BillingPeriod
{
  public DateOnly StartDate { get; }
  public DateOnly EndDateExclusive { get; }

  private BillingPeriod(
      DateOnly startDate,
      DateOnly endDateExclusive)
  {
    StartDate = startDate;
    EndDateExclusive = endDateExclusive;
  }

  public static BillingPeriod ForMonth(int year, int month)
  {
    if (year < 1 || year > 9999)
    {
      throw new ArgumentOutOfRangeException(nameof(year));
    }

    if (month < 1 || month > 12)
    {
      throw new ArgumentOutOfRangeException(nameof(month));
    }

    var start = new DateOnly(year, month, 1);
    var endExclusive = start.AddMonths(1);

    return new BillingPeriod(start, endExclusive);
  }

  public bool Contains(DateOnly date) =>
      date >= StartDate && date < EndDateExclusive;

  public override string ToString() =>
      $"{StartDate:yyyy-MM}";
}
