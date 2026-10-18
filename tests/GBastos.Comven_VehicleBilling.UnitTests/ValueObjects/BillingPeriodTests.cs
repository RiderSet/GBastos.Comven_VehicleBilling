using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.UnitTests.ValueObjects;

public sealed class BillingPeriodTests
{
  [Fact]
  public void ForMonth_DeveCriarPeriodoCorreto()
  {
    var period = BillingPeriod.ForMonth(2026, 10);

    Assert.Equal(new DateOnly(2026, 10, 1), period.StartDate);
    Assert.Equal(
        new DateOnly(2026, 11, 1),
        period.EndDateExclusive);
  }

  [Fact]
  public void ForMonth_DeveRejeitarMesInvalido()
  {
    Assert.Throws<ArgumentOutOfRangeException>(
        () => BillingPeriod.ForMonth(2026, 13));
  }

  [Fact]
  public void ForMonth_DeveRejeitarAnoInvalido()
  {
    Assert.Throws<ArgumentOutOfRangeException>(
        () => BillingPeriod.ForMonth(0, 10));
  }

  [Fact]
  public void ForMonth_DeveTratarFevereiroDeAnoBissexto()
  {
    var period = BillingPeriod.ForMonth(2028, 2);

    Assert.Equal(new DateOnly(2028, 2, 1), period.StartDate);
    Assert.Equal(
        new DateOnly(2028, 3, 1),
        period.EndDateExclusive);
  }

  [Fact]
  public void Contains_DeveIncluirPrimeiroDiaDoMes()
  {
    var period = BillingPeriod.ForMonth(2026, 10);

    Assert.True(period.Contains(new DateOnly(2026, 10, 1)));
  }

  [Fact]
  public void Contains_DeveIncluirUltimoDiaDoMes()
  {
    var period = BillingPeriod.ForMonth(2026, 10);

    Assert.True(period.Contains(new DateOnly(2026, 10, 31)));
  }

  [Fact]
  public void Contains_NaoDeveIncluirPrimeiroDiaDoMesSeguinte()
  {
    var period = BillingPeriod.ForMonth(2026, 10);

    Assert.False(period.Contains(new DateOnly(2026, 11, 1)));
  }

  [Fact]
  public void ToString_DeveRetornarAnoEMes()
  {
    var period = BillingPeriod.ForMonth(2026, 10);

    Assert.Equal("2026-10", period.ToString());
  }
}
