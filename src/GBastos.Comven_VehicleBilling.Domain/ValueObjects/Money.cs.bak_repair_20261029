using System.Globalization;

namespace Comven_VehicleBilling.Domain.ValueObjects;

public sealed record Money
{
  public decimal Amount { get; }

  private Money(decimal amount)
  {
    Amount = amount;
  }

  public static Money Zero => new(0m);

  public static Money Of(decimal amount)
  {
    if (decimal.Round(amount, 2) != amount)
    {
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor monetário deve possuir no máximo duas casas decimais.");
    }

    if (amount < 0m)
    {
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor monetário não pode ser negativo.");
    }

    return new Money(amount);
  }

  public static Money Positive(decimal amount)
  {
    if (amount <= 0m)
    {
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor monetário deve ser maior que zero.");
    }

    return Of(amount);
  }

  public Money Add(Money other)
  {
    ArgumentNullException.ThrowIfNull(other);

    return Of(checked(Amount + other.Amount));
  }

  public Money Subtract(Money other)
  {
    ArgumentNullException.ThrowIfNull(other);

    if (other.Amount > Amount)
    {
      throw new InvalidOperationException(
          "O saldo não pode ficar negativo.");
    }

    return Of(checked(Amount - other.Amount));
  }

  public bool IsZero => Amount == 0m;

  public bool IsPositive => Amount > 0m;

  public static Money operator +(Money left, Money right)
      => left.Add(right);

  public static Money operator -(Money left, Money right)
      => left.Subtract(right);

  public static bool operator >(Money left, Money right)
      => left.Amount > right.Amount;

  public static bool operator <(Money left, Money right)
      => left.Amount < right.Amount;

  public static bool operator >=(Money left, Money right)
      => left.Amount >= right.Amount;

  public static bool operator <=(Money left, Money right)
      => left.Amount <= right.Amount;

  public override string ToString()
      => Amount.ToString(
          "N2",
          CultureInfo.GetCultureInfo("pt-BR"));
}
