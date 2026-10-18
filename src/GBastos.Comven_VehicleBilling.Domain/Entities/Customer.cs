using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class Customer
{
  public Guid Id { get; private set; }
  public string Name { get; private set; } = string.Empty;
  public CustomerPlan Plan { get; private set; }
  public decimal Balance { get; private set; }
  public bool IsActive { get; private set; }

  private Customer() { }

  public Customer(
      Guid id,
      string name,
      CustomerPlan plan,
      decimal initialBalance)
  {
    if (id == Guid.Empty)
      throw new ArgumentException("Customer ID is required.");

    if (string.IsNullOrWhiteSpace(name))
      throw new ArgumentException("Customer name is required.");

    if (initialBalance < 0)
      throw new ArgumentOutOfRangeException(
          nameof(initialBalance));

    if (plan == CustomerPlan.Postpaid && initialBalance != 0)
      throw new ArgumentException(
          "Postpaid customers do not use a prepaid balance.");

    Id = id;
    Name = name.Trim();
    Plan = plan;
    Balance = initialBalance;
    IsActive = true;
  }

  public void Debit(decimal amount)
  {
    if (Plan != CustomerPlan.Prepaid)
      throw new InvalidOperationException(
          "Only prepaid customers have a balance.");

    if (amount <= 0)
      throw new ArgumentOutOfRangeException(nameof(amount));

    if (Balance < amount)
      throw new InvalidOperationException(
          "INSUFFICIENT_BALANCE");

    Balance -= amount;
  }

  public void Credit(decimal amount)
  {
    if (amount <= 0)
      throw new ArgumentOutOfRangeException(nameof(amount));

    Balance += amount;
  }
}
