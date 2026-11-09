using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Document { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public CustomerPlan Plan { get; private set; }
    public Money Balance { get; private set; } = Money.Zero;
    public DateTimeOffset CreatedAt { get; private set; }

    private Customer()
    {
    }

    public Customer(
        Guid id,
        string name,
        CustomerPlan plan,
        Money initialBalance,
        DateTimeOffset createdAt,
        string document = "",
        string email = "")
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "O ID do cliente e obrigatorio.", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome do cliente e obrigatorio.", nameof(name));

        if (!Enum.IsDefined(plan))
            throw new ArgumentOutOfRangeException(
                nameof(plan), "Plano de cliente invalido.");

        ArgumentNullException.ThrowIfNull(initialBalance);

        if (plan == CustomerPlan.Postpaid && !initialBalance.IsZero)
            throw new ArgumentException(
                "Clientes pos-pagos nao podem possuir saldo pre-pago.",
                nameof(initialBalance));

        if (plan == CustomerPlan.Prepaid && initialBalance.Amount < 0)
            throw new ArgumentException(
                "O saldo inicial nao pode ser negativo.",
                nameof(initialBalance));

        Id = id;
        Name = name.Trim();
        Document = document?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Plan = plan;
        Balance = initialBalance;
        CreatedAt = createdAt;
    }

    public void Debit(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (Plan != CustomerPlan.Prepaid)
            throw new InvalidOperationException(
                "Debitos de saldo so sao permitidos para clientes pre-pagos.");

        if (amount.IsZero || amount.Amount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount), "O valor do debito deve ser maior que zero.");

        if (amount.Amount > Balance.Amount)
            throw new InvalidOperationException(
                "Saldo insuficiente para realizar a operacao.");

        Balance = Balance.Subtract(amount);
    }

    public void Credit(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (Plan != CustomerPlan.Prepaid)
            throw new InvalidOperationException(
                "Creditos de saldo so sao permitidos para clientes pre-pagos.");

        if (amount.IsZero || amount.Amount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount), "O valor do credito deve ser maior que zero.");

        Balance = Balance.Add(amount);
    }
}
