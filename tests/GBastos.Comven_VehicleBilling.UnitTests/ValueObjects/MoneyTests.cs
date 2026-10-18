using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.UnitTests.ValueObjects;

public sealed class MoneyTests
{
  [Theory]
  [InlineData(12.50)]
  [InlineData(45.00)]
  [InlineData(8.90)]
  public void Of_ComValorValido_DeveCriarMoney(decimal amount)
  {
    var money = Money.Of(amount);

    Assert.Equal(amount, money.Amount);
  }

  [Fact]
  public void Zero_DeveRepresentarValorZero()
  {
    var money = Money.Zero;

    Assert.Equal(0m, money.Amount);
    Assert.True(money.IsZero);
  }

  [Theory]
  [InlineData(-1.00)]
  [InlineData(1.234)]
  public void Of_ComValorInvalido_DeveLancarExcecao(decimal amount)
  {
    Assert.Throws<ArgumentOutOfRangeException>(
        () => Money.Of(amount));
  }

  [Fact]
  public void Positive_ComZero_DeveLancarExcecao()
  {
    Assert.Throws<ArgumentOutOfRangeException>(
        () => Money.Positive(0m));
  }

  [Fact]
  public void Add_DeveSomarValores()
  {
    var first = Money.Of(12.50m);
    var second = Money.Of(45.00m);

    var result = first.Add(second);

    Assert.Equal(57.50m, result.Amount);
  }

  [Fact]
  public void Subtract_ComSaldoSuficiente_DeveSubtrair()
  {
    var balance = Money.Of(100m);
    var price = Money.Of(12.50m);

    var result = balance.Subtract(price);

    Assert.Equal(87.50m, result.Amount);
  }

  [Fact]
  public void Subtract_ComSaldoInsuficiente_DeveLancarExcecao()
  {
    var balance = Money.Of(10m);
    var price = Money.Of(12.50m);

    Assert.Throws<InvalidOperationException>(
        () => balance.Subtract(price));
  }

  [Fact]
  public void DoisMoneyComMesmoValor_DevemSerIguais()
  {
    var first = Money.Of(12.50m);
    var second = Money.Of(12.50m);

    Assert.Equal(first, second);
  }

  [Fact]
  public void ToString_DeveUsarFormatoBrasileiro()
  {
    var money = Money.Of(12.50m);

    Assert.Equal("12,50", money.ToString());
  }
}
