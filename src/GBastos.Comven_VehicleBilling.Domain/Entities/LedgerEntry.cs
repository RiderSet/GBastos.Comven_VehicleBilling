using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class LedgerEntry
{
  public Guid Id { get; private set; }
  public Guid CustomerId { get; private set; }

  public Guid? OperationId { get; private set; }
  public Guid? BalanceCreditId { get; private set; }

  public Money Amount { get; private set; } = Money.Zero;
  public LedgerEntryType Type { get; private set; }
  public string Description { get; private set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; private set; }

  public Customer Customer { get; private set; } = null!;

  private LedgerEntry()
  {
  }

  // Lançamento de débito originado por uma operação.
  public LedgerEntry(
      Guid id,
      Guid customerId,
      Guid operationId,
      Money amount,
      LedgerEntryType type,
      string description,
      DateTimeOffset createdAt)
  {
    ValidateCommon(id, customerId, amount, type, description);

    if (operationId == Guid.Empty)
      throw new ArgumentException(
          "O ID da operação é obrigatório.", nameof(operationId));

    if (type != LedgerEntryType.Debit)
      throw new ArgumentException(
          "Uma operação deve gerar um lançamento de débito.",
          nameof(type));

    if (createdAt == default)
      throw new ArgumentException(
          "A data do lançamento é obrigatória.",
          nameof(createdAt));

    Id = id;
    CustomerId = customerId;
    OperationId = operationId;
    BalanceCreditId = null;
    Amount = amount;
    Type = type;
    Description = description.Trim();
    CreatedAt = createdAt;
  }

  // Fábrica para créditos de saldo.
  public static LedgerEntry CreateCredit(
      Guid id,
      Guid customerId,
      Guid balanceCreditId,
      Money amount,
      string description,
      DateTimeOffset createdAt)
  {
    ValidateCommon(
        id,
        customerId,
        amount,
        LedgerEntryType.Credit,
        description);

    if (balanceCreditId == Guid.Empty)
      throw new ArgumentException(
          "O ID do crédito de saldo é obrigatório.",
          nameof(balanceCreditId));

    if (createdAt == default)
      throw new ArgumentException(
          "A data do lançamento é obrigatória.",
          nameof(createdAt));

    return new LedgerEntry
    {
      Id = id,
      CustomerId = customerId,
      OperationId = null,
      BalanceCreditId = balanceCreditId,
      Amount = amount,
      Type = LedgerEntryType.Credit,
      Description = description.Trim(),
      CreatedAt = createdAt
    };
  }

  private static void ValidateCommon(
      Guid id,
      Guid customerId,
      Money amount,
      LedgerEntryType type,
      string description)
  {
    if (id == Guid.Empty)
      throw new ArgumentException(
          "O ID do lançamento é obrigatório.", nameof(id));

    if (customerId == Guid.Empty)
      throw new ArgumentException(
          "O ID do cliente é obrigatório.", nameof(customerId));

    ArgumentNullException.ThrowIfNull(amount);

    if (amount.IsZero || amount.Amount < 0)
      throw new ArgumentOutOfRangeException(
          nameof(amount), "O valor deve ser maior que zero.");

    if (!Enum.IsDefined(type))
      throw new ArgumentOutOfRangeException(
          nameof(type), "Tipo de lançamento inválido.");

    if (string.IsNullOrWhiteSpace(description))
      throw new ArgumentException(
          "A descrição do lançamento é obrigatória.",
          nameof(description));

    if (description.Trim().Length > 250)
      throw new ArgumentException(
          "A descrição aceita até 250 caracteres.",
          nameof(description));
  }
}
