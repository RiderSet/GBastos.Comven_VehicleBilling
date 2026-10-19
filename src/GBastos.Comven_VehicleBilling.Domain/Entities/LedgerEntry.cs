using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class LedgerEntry
{
  public Guid Id { get; private set; }
  public Guid OperationId { get; private set; }
  public Guid CustomerId { get; private set; }
  public LedgerEntryType Type { get; private set; }
  public Money Amount { get; private set; } = Money.Zero;
  public DateTimeOffset CreatedAt { get; private set; }
  public Operation Operation { get; private set; } = null!;
  public Customer Customer { get; private set; } = null!;

  // Construtor utilizado pelo Entity Framework Core.
  private LedgerEntry()
  {
  }

  public LedgerEntry(
      Guid id,
      Guid operationId,
      Guid customerId,
      LedgerEntryType type,
      Money amount,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
    {
      throw new ArgumentException(
          "O ID do lançamento é obrigatório.",
          nameof(id));
    }

    if (operationId == Guid.Empty)
    {
      throw new ArgumentException(
          "O ID da operação é obrigatório.",
          nameof(operationId));
    }

    if (customerId == Guid.Empty)
    {
      throw new ArgumentException(
          "O ID do cliente é obrigatório.",
          nameof(customerId));
    }

    if (!Enum.IsDefined(type))
    {
      throw new ArgumentOutOfRangeException(
          nameof(type),
          "Tipo de lançamento inválido.");
    }

    ArgumentNullException.ThrowIfNull(amount);

    if (amount.IsZero)
    {
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor do lançamento deve ser maior que zero.");
    }

    Id = id;
    OperationId = operationId;
    CustomerId = customerId;
    Type = type;
    Amount = amount;
    CreatedAt = createdAt;
  }
}
