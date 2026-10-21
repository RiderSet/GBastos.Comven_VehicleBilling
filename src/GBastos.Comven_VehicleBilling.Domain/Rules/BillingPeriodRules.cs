using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Rules;

public static class BillingPeriodRules
{
  /// <summary>
  /// Retorna o período correspondente ao mês atual.
  /// </summary>
  public static BillingPeriod CurrentMonth(
      DateTimeOffset referenceDate)
  {
    return BillingPeriod.ForMonth(
        referenceDate.Year,
        referenceDate.Month);
  }

  /// <summary>
  /// Retorna o período correspondente ao mês anterior.
  /// Exemplo:
  /// 2026-07-01 -> 2026-06.
  /// </summary>
  public static BillingPeriod PreviousMonth(
      DateTimeOffset referenceDate)
  {
    var previousMonth = referenceDate.AddMonths(-1);

    return BillingPeriod.ForMonth(
        previousMonth.Year,
        previousMonth.Month);
  }

  /// <summary>
  /// Verifica se uma data pertence ao período informado.
  /// </summary>
  public static bool IsInsidePeriod(
      DateOnly date,
      BillingPeriod period)
  {
    ArgumentNullException.ThrowIfNull(period);

    return date >= period.StartDate
        && date < period.EndDateExclusive;
  }

  /// <summary>
  /// Retorna o último dia do período.
  /// </summary>
  public static DateOnly GetLastDay(
      BillingPeriod period)
  {
    ArgumentNullException.ThrowIfNull(period);

    return period.EndDateExclusive.AddDays(-1);
  }

  /// <summary>
  /// Indica se o período já pode ser faturado.
  /// Exemplo:
  /// Período 2026-06 pode ser faturado em 2026-07-01.
  /// </summary>
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
