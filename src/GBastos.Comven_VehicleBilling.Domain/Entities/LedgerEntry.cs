using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class LedgerEntry
{
  public Guid Id { get; private set; }
  public Guid CustomerId { get; private set; }
  public Guid OperationId { get; private set; }
  public Money Amount { get; private set; } = Money.Zero;
  public LedgerEntryType Type { get; private set; }
  public string Description { get; private set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; private set; }

  public Customer Customer { get; private set; } = null!;

  private LedgerEntry()
  {
  }

  public LedgerEntry(
      Guid id,
      Guid customerId,
      Guid operationId,
      Money amount,
      LedgerEntryType type,
      string description,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
      throw new ArgumentException(
          "O ID do lançamento é obrigatório.", nameof(id));

    if (customerId == Guid.Empty)
      throw new ArgumentException(
          "O ID do cliente é obrigatório.", nameof(customerId));

    if (operationId == Guid.Empty)
      throw new ArgumentException(
          "O ID da operação é obrigatório.", nameof(operationId));

    ArgumentNullException.ThrowIfNull(amount);

    if (amount.IsZero)
      throw new ArgumentException(
          "O valor do lançamento deve ser maior que zero.", nameof(amount));

    if (string.IsNullOrWhiteSpace(description))
      throw new ArgumentException(
          "A descrição do lançamento é obrigatória.", nameof(description));

    Id = id;
    CustomerId = customerId;
    OperationId = operationId;
    Amount = amount;
    Type = type;
    Description = description.Trim();
    CreatedAt = createdAt;
  }
}
