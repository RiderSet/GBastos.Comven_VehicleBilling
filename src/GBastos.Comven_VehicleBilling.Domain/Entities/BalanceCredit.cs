using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class BalanceCredit
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string ExternalReference { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = Money.Zero;
    public DateTimeOffset CreatedAt { get; private set; }

    public Customer Customer { get; private set; } = null!;

    private BalanceCredit()
    {
    }

    public BalanceCredit(
        Guid id,
        Guid customerId,
        string externalReference,
        Money amount,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "O identificador da recarga é obrigatório.", nameof(id));

        if (customerId == Guid.Empty)
            throw new ArgumentException(
                "O identificador do cliente é obrigatório.", nameof(customerId));

        if (string.IsNullOrWhiteSpace(externalReference))
            throw new ArgumentException(
                "A referência externa é obrigatória.", nameof(externalReference));

        ArgumentNullException.ThrowIfNull(amount);

        if (amount.IsZero)
            throw new ArgumentOutOfRangeException(
                nameof(amount), "O valor da recarga deve ser maior que zero.");

        Id = id;
        CustomerId = customerId;
        ExternalReference = externalReference.Trim();
        Amount = amount;
        CreatedAt = createdAt;
    }
}