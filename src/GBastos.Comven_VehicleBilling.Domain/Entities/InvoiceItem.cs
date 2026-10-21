using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class InvoiceItem
{
  public Guid Id { get; private set; }

  public Guid InvoiceId { get; private set; }

  public Guid OperationId { get; private set; }

  public Money Amount { get; private set; } = Money.Zero;

  public DateTimeOffset CreatedAt { get; private set; }

  // Propriedade de navegação para o Entity Framework Core.
  public Invoice Invoice { get; private set; } = null!;

  // Construtor utilizado pelo Entity Framework Core.
  private InvoiceItem()
  {
  }

  public InvoiceItem(
      Guid id,
      Guid invoiceId,
      Guid operationId,
      Money amount,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
      throw new ArgumentException(
          "O ID do item da fatura é obrigatório.", nameof(id));

    if (invoiceId == Guid.Empty)
      throw new ArgumentException(
          "O ID da fatura é obrigatório.", nameof(invoiceId));

    if (operationId == Guid.Empty)
      throw new ArgumentException(
          "O ID da operação é obrigatório.", nameof(operationId));

    ArgumentNullException.ThrowIfNull(amount);

    if (amount.IsZero)
      throw new ArgumentException(
          "O valor do item deve ser maior que zero.", nameof(amount));

    Id = id;
    InvoiceId = invoiceId;
    OperationId = operationId;
    Amount = amount;
    CreatedAt = createdAt;
  }
}
