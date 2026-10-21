namespace Comven_VehicleBilling.Domain.ValueObjects;

public sealed record BillingPeriod
{
  public DateOnly StartDate { get; }

  public DateOnly EndDateExclusive { get; }

  public int Year => StartDate.Year;

  public int Month => StartDate.Month;

  private BillingPeriod(
      DateOnly startDate,
      DateOnly endDateExclusive)
  {
    if (endDateExclusive <= startDate)
    {
      throw new ArgumentException(
          "O final do período deve ser maior que o início.");
    }

    StartDate = startDate;
    EndDateExclusive = endDateExclusive;
  }

  public static BillingPeriod ForMonth(
      int year,
      int month)
  {
    if (year < 1 || year > 9999)
    {
      throw new ArgumentOutOfRangeException(nameof(year));
    }

    if (month < 1 || month > 12)
    {
      throw new ArgumentOutOfRangeException(nameof(month));
    }

    var startDate = new DateOnly(year, month, 1);

    var endDateExclusive =
        startDate.AddMonths(1);

    return new BillingPeriod(
        startDate,
        endDateExclusive);
  }

  public static BillingPeriod PreviousMonth(
      DateTimeOffset currentDate)
  {
    var previousMonth =
        currentDate.AddMonths(-1);

    return ForMonth(
        previousMonth.Year,
        previousMonth.Month);
  }

  public bool Contains(DateOnly date)
  {
    return date >= StartDate
        && date < EndDateExclusive;
  }

  public override string ToString()
  {
    return $"{Year:D4}-{Month:D2}";
  }

}
