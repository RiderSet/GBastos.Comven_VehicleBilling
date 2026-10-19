using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class InvoiceItem
{
  public Guid InvoiceId { get; private set; }

  public Guid OperationId { get; private set; }

  public Money Amount { get; private set; } = Money.Zero;

  public Invoice Invoice { get; private set; } = null!;

  public Operation Operation { get; private set; } = null!;

  // Construtor utilizado pelo Entity Framework Core.
  private InvoiceItem()
  {
  }

  public InvoiceItem(
      Guid invoiceId,
      Guid operationId,
      Money amount)
  {
    if (invoiceId == Guid.Empty)
    {
      throw new ArgumentException(
          "O ID da fatura é obrigatório.",
          nameof(invoiceId));
    }

    if (operationId == Guid.Empty)
    {
      throw new ArgumentException(
          "O ID da operação é obrigatório.",
          nameof(operationId));
    }

    ArgumentNullException.ThrowIfNull(amount);

    if (amount.IsZero)
    {
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor do item deve ser maior que zero.");
    }

    InvoiceId = invoiceId;
    OperationId = operationId;
    Amount = amount;
  }
}
