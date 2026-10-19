using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.Rules;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class Operation
{
  public Guid Id { get; private set; }
  public Guid CustomerId { get; private set; }
  public OperationType Type { get; private set; }
  public Money Amount { get; private set; } = Money.Zero;
  public OperationStatus Status { get; private set; }
  public string RequestHash { get; private set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; private set; }
  public Guid? InvoiceId { get; private set; }
  public Customer Customer { get; private set; } = null!;
  public Invoice? Invoice { get; private set; }

  // Construtor utilizado pelo Entity Framework Core.
  private Operation()
  {
  }

  private Operation(
      Guid id,
      Guid customerId,
      OperationType type,
      Money amount,
      OperationStatus status,
      string requestHash,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
      throw new ArgumentException("O ID da operação é obrigatório.", nameof(id));

    if (customerId == Guid.Empty)
      throw new ArgumentException("O ID do cliente é obrigatório.", nameof(customerId));

    ArgumentNullException.ThrowIfNull(amount);

    if (amount.IsZero)
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor da operação deve ser maior que zero.");

    if (!Enum.IsDefined(type))
      throw new ArgumentOutOfRangeException(nameof(type), "Tipo de operação inválido.");

    if (status is not OperationStatus.Charged
        and not OperationStatus.AwaitingInvoice)
    {
      throw new ArgumentException(
          "O estado inicial deve ser Charged ou AwaitingInvoice.",
          nameof(status));
    }

    if (string.IsNullOrWhiteSpace(requestHash))
      throw new ArgumentException(
          "A chave de idempotência da operação é obrigatória.",
          nameof(requestHash));

    Id = id;
    CustomerId = customerId;
    Type = type;
    Amount = amount;
    Status = status;
    RequestHash = requestHash;
    CreatedAt = createdAt;
  }

  public static Operation Create(
      Guid id,
      Customer customer,
      OperationType type,
      string requestHash,
      DateTimeOffset createdAt)
  {
    ArgumentNullException.ThrowIfNull(customer);

    var amount = Money.Of(OperationPricing.GetPrice(type));

    var status = customer.Plan switch
    {
      CustomerPlan.Prepaid => OperationStatus.Charged,
      CustomerPlan.Postpaid => OperationStatus.AwaitingInvoice,
      _ => throw new ArgumentOutOfRangeException(
          nameof(customer),
          "Plano de cliente inválido.")
    };

    return new Operation(
        id,
        customer.Id,
        type,
        amount,
        status,
        requestHash,
        createdAt);
  }

  public void Refund()
  {
    if (Status != OperationStatus.Charged)
    {
      throw new InvalidOperationException(
          "Somente operações pré-pagas cobradas podem ser reembolsadas.");
    }

    Status = OperationStatus.Refunded;
  }

  public void MarkFailed()
  {
    if (Status is not OperationStatus.Charged
        and not OperationStatus.AwaitingInvoice)
    {
      throw new InvalidOperationException(
          "A operação não pode falhar a partir do estado atual.");
    }

    Status = OperationStatus.Failed;
  }

  public void AssignToInvoice(Guid invoiceId)
  {
    if (Status != OperationStatus.AwaitingInvoice)
    {
      throw new InvalidOperationException(
          "Somente operações aguardando faturamento podem ser incluídas em uma fatura.");
    }

    if (invoiceId == Guid.Empty)
      throw new ArgumentException("O ID da fatura é obrigatório.", nameof(invoiceId));

    InvoiceId = invoiceId;
    Status = OperationStatus.Invoiced;
  }
}
