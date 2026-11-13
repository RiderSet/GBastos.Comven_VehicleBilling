using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Rules;

public static class BillingPeriodRules
{
    public static BillingPeriod CurrentMonth(
        DateTimeOffset referenceDate)
    {
        return BillingPeriod.ForMonth(
            referenceDate.Year,
            referenceDate.Month);
    }

    public static BillingPeriod PreviousMonth(
        DateTimeOffset referenceDate)
    {
        var previousMonth = referenceDate.AddMonths(-1);

        return BillingPeriod.ForMonth(
            previousMonth.Year,
            previousMonth.Month);
    }

    public static bool IsInsidePeriod(
        DateOnly date,
        BillingPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        return date >= period.StartDate
            && date < period.EndDateExclusive;
    }

    public static DateOnly LastDayOfPeriod(
        BillingPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        return period.EndDateExclusive.AddDays(-1);
    }

    public static bool CanGenerateInvoice(
        BillingPeriod period,
        DateTimeOffset currentDate)
    {
        ArgumentNullException.ThrowIfNull(period);

        var currentMonthStart = new DateOnly(
            currentDate.Year,
            currentDate.Month,
            1);

        return currentMonthStart >= period.EndDateExclusive;
    }
}
