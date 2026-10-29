using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class Operation
{
  public Guid Id { get; private set; }
  public Guid ExternalOperationId { get; private set; }
  public Guid CustomerId { get; private set; }
  public OperationType Type { get; private set; }
  public Money Amount { get; private set; } = Money.Zero;
  public OperationStatus Status { get; private set; }
  public DateTimeOffset CreatedAt { get; private set; }
  public DateTimeOffset? RefundedAt { get; private set; }
  public DateTimeOffset? InvoicedAt { get; private set; }
  public Guid? InvoiceId { get; private set; }
  public Customer Customer { get; private set; } = null!;
  public Invoice? Invoice { get; private set; }
  public string RequestHash { get; private set; } = string.Empty;

  private Operation()
  {
    // Construtor utilizado pelo Entity Framework Core.
  }

  public Operation(
      Guid id,
      Guid externalOperationId,
      Guid customerId,
      OperationType type,
      Money amount,
      string requestHash,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
      throw new ArgumentException(
          "O ID da operação é obrigatório.", nameof(id));

    if (externalOperationId == Guid.Empty)
      throw new ArgumentException(
          "O ID externo da operação é obrigatório.",
          nameof(externalOperationId));

    if (customerId == Guid.Empty)
      throw new ArgumentException(
          "O ID do cliente é obrigatório.", nameof(customerId));

    if (!Enum.IsDefined(type))
      throw new ArgumentOutOfRangeException(
          nameof(type), "O tipo da operação é inválido.");

    ArgumentNullException.ThrowIfNull(amount);

    if (!amount.IsPositive)
      throw new ArgumentOutOfRangeException(
          nameof(amount), "O valor deve ser maior que zero.");

    if (string.IsNullOrWhiteSpace(requestHash))
      throw new ArgumentException(
          "O hash da solicitação é obrigatório.",
          nameof(requestHash));

    requestHash = requestHash.Trim();

    if (requestHash.Length > 200)
      throw new ArgumentException(
          "O hash não pode ultrapassar 200 caracteres.",
          nameof(requestHash));

    Id = id;
    ExternalOperationId = externalOperationId;
    CustomerId = customerId;
    Type = type;
    Amount = amount;
    RequestHash = requestHash;
    CreatedAt = createdAt;
    Status = OperationStatus.Pending;
  }

  public void MarkCharged()
  {
    EnsurePending();
    Status = OperationStatus.Charged;
  }

  public void MarkAwaitingInvoice()
  {
    EnsurePending();
    Status = OperationStatus.AwaitingInvoice;
  }

  public void MarkFailed()
  {
    EnsurePending();
    Status = OperationStatus.Failed;
  }

  public void MarkRefunded(DateTimeOffset refundedAt)
  {
    if (Status != OperationStatus.Charged)
      throw new InvalidOperationException(
          "Somente operações cobradas podem ser reembolsadas.");

    if (refundedAt == default)
      throw new ArgumentException(
          "A data do reembolso é obrigatória.",
          nameof(refundedAt));

    RefundedAt = refundedAt;
    Status = OperationStatus.Refunded;
  }

  public void MarkInvoiced(Guid invoiceId, DateTimeOffset invoicedAt)
  {
    if (Status != OperationStatus.AwaitingInvoice)
      throw new InvalidOperationException(
          "Somente operações aguardando faturamento podem ser faturadas.");

    if (invoiceId == Guid.Empty)
      throw new ArgumentException(
          "O ID da fatura é obrigatório.", nameof(invoiceId));

    if (invoicedAt == default)
      throw new ArgumentException(
          "A data do faturamento é obrigatória.",
          nameof(invoicedAt));

    InvoiceId = invoiceId;
    InvoicedAt = invoicedAt;
    Status = OperationStatus.Invoiced;
  }

  private void EnsurePending()
  {
    if (Status != OperationStatus.Pending)
      throw new InvalidOperationException(
          "A operação não está pendente.");
  }
}
