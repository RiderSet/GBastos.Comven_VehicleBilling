using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class BillingOperation
{
  public Guid Id { get; private set; }
  public Guid CustomerId { get; private set; }
  public OperationType Type { get; private set; }
  public string Provider { get; private set; } = string.Empty;
  public decimal Amount { get; private set; }
  public OperationStatus Status { get; private set; }
  public DateTimeOffset CreatedAt { get; private set; }
  public DateTimeOffset? CompletedAt { get; private set; }
  public Guid? InvoiceId { get; private set; }

  // Construtor privado utilizado pelo Entity Framework Core.
  private BillingOperation()
  {
  }

  public BillingOperation(
      Guid id,
      Guid customerId,
      OperationType type,
      decimal amount,
      OperationStatus status,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
      throw new ArgumentException("O ID da operação é obrigatório.", nameof(id));

    if (customerId == Guid.Empty)
      throw new ArgumentException("O ID do cliente é obrigatório.", nameof(customerId));

    if (amount <= 0m)
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor da operação deve ser maior que zero.");

    if (decimal.Round(amount, 2) != amount)
      throw new ArgumentOutOfRangeException(
          nameof(amount),
          "O valor da operação deve ter no máximo duas casas decimais.");

    if (!Enum.IsDefined(type))
      throw new ArgumentOutOfRangeException(nameof(type), "Tipo de operação inválido.");

    if (status is not OperationStatus.Charged
        and not OperationStatus.AwaitingInvoice)
    {
      throw new ArgumentException(
          "Uma nova operação deve iniciar como Charged ou AwaitingInvoice.",
          nameof(status));
    }

    Id = id;
    CustomerId = customerId;
    Type = type;
    Amount = amount;
    Status = status;
    CreatedAt = createdAt;
  }

  public void Complete(string provider, DateTimeOffset completedAt)
  {
    if (string.IsNullOrWhiteSpace(provider))
      throw new ArgumentException("O fornecedor é obrigatório.", nameof(provider));

    if (Status is not OperationStatus.Charged
        and not OperationStatus.AwaitingInvoice)
    {
      throw new InvalidOperationException(
          "A operação não pode ser concluída a partir do estado atual.");
    }

    Provider = provider.Trim();
    CompletedAt = completedAt;

    // Operações pré-pagas permanecem Charged.
    // Operações pós-pagas passam para AwaitingInvoice.
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
    CompletedAt = DateTimeOffset.UtcNow;
  }

  public void MarkRefunded()
  {
    if (Status != OperationStatus.Charged)
    {
      throw new InvalidOperationException(
          "Somente operações pré-pagas cobradas podem ser reembolsadas.");
    }

    Status = OperationStatus.Refunded;
    CompletedAt = DateTimeOffset.UtcNow;
  }

  public void MarkInvoiced(Guid invoiceId)
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
