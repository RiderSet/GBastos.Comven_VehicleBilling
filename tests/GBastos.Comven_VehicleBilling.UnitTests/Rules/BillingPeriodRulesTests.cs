using Comven_VehicleBilling.Domain.Rules;
using Comven_VehicleBilling.Domain.ValueObjects;
using Xunit;

namespace GBastos.Comven_VehicleBilling.UnitTests.Rules;

public class BillingPeriodRulesTests
{
    private static readonly DateTimeOffset CurrentDate =
        new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2026, 9, true)]
    [InlineData(2026, 10, true)]
    [InlineData(2026, 11, false)]
    [InlineData(2026, 12, false)]
    public void CanGenerateInvoice_ShouldAllowOnlyCompletedPeriods(
        int year,
        int month,
        bool expected)
    {
        var period = BillingPeriod.ForMonth(year, month);

        var result = BillingPeriodRules.CanGenerateInvoice(
            period,
            CurrentDate);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanGenerateInvoice_ShouldAllowPreviousYearDecemberInJanuary()
    {
        var period = BillingPeriod.ForMonth(2026, 12);
        var januaryReference = new DateTimeOffset(
            2027, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var result = BillingPeriodRules.CanGenerateInvoice(
            period,
            januaryReference);

        Assert.True(result);
    }
}
