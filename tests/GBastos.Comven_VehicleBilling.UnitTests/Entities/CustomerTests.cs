using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.UnitTests.Entities;

public sealed class CustomerTests
{
  [Fact]
  public void Credit_WhenPrepaid_ShouldIncreaseBalance()
  {
    var customer = new Customer(
        Guid.NewGuid(),
        "Cliente teste",
        CustomerPlan.Prepaid,
        Money.Zero,
        DateTimeOffset.UtcNow);

    customer.Credit(Money.Positive(10m));

    Assert.Equal(10m, customer.Balance.Amount);
  }

  [Fact]
  public void Credit_WhenPostpaid_ShouldThrowAndKeepBalanceUnchanged()
  {
    var customer = new Customer(
        Guid.NewGuid(),
        "Cliente pos-pago",
        CustomerPlan.Postpaid,
        Money.Zero,
        DateTimeOffset.UtcNow);

    Assert.Throws<InvalidOperationException>(
        () => customer.Credit(Money.Positive(10m)));

    Assert.Equal(0m, customer.Balance.Amount);
  }

  [Fact]
  public void Constructor_WhenPostpaidWithInitialBalance_ShouldThrow()
  {
    Assert.Throws<ArgumentException>(() =>
        new Customer(
            Guid.NewGuid(),
            "Cliente pos-pago",
            CustomerPlan.Postpaid,
            Money.Positive(10m),
            DateTimeOffset.UtcNow));
  }
}
