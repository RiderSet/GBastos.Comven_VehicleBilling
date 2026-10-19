using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class Customer
{
  public Guid Id { get; private set; }
  public string Name { get; private set; } = string.Empty;
  public CustomerPlan Plan { get; private set; }
  public Money Balance { get; private set; } = Money.Zero;
  public DateTimeOffset CreatedAt { get; private set; }

  // Construtor utilizado pelo Entity Framework Core.
  private Customer()
  {
  }

  public Customer(
      Guid id,
      string name,
      CustomerPlan plan,
      Money initialBalance,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
    {
      throw new ArgumentException(
          "O ID do cliente é obrigatório.",
          nameof(id));
    }

    if (string.IsNullOrWhiteSpace(name))
    {
      throw new ArgumentException(
          "O nome do cliente é obrigatório.",
          nameof(name));
    }

    if (!Enum.IsDefined(plan))
    {
      throw new ArgumentOutOfRangeException(
          nameof(plan),
          "Plano de cliente inválido.");
    }

    ArgumentNullException.ThrowIfNull(initialBalance);

    if (plan == CustomerPlan.Postpaid && !initialBalance.IsZero)
    {
      throw new ArgumentException(
          "Clientes pós-pagos não podem possuir saldo pré-pago.",
          nameof(initialBalance));
    }

    Id = id;
    Name = name.Trim();
    Plan = plan;
    Balance = initialBalance;
    CreatedAt = createdAt;
  }

  public void Debit(Money amount)
  {
    ArgumentNullException.ThrowIfNull(amount);

    if (Plan != CustomerPlan.Prepaid)
    {
      throw new InvalidOperationException(
          "Débitos de saldo só são permitidos para clientes pré-pagos.");
    }

    if (amount.IsZero)
    {
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor do débito deve ser maior que zero.");
    }

    if (amount.Amount > Balance.Amount)
    {
      throw new InvalidOperationException(
          "Saldo insuficiente para realizar a operação.");
    }

    Balance = Balance.Subtract(amount);
  }

  public void Credit(Money amount)
  {
    ArgumentNullException.ThrowIfNull(amount);

    if (Plan != CustomerPlan.Prepaid)
    {
      throw new InvalidOperationException(
          "Créditos de saldo só são permitidos para clientes pré-pagos.");
    }

    if (amount.IsZero)
    {
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor do crédito deve ser maior que zero.");
    }

    Balance = Balance.Add(amount);
  }
}
