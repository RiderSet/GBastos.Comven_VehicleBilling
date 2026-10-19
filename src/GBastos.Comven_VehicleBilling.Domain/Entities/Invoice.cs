using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class Invoice
{
  private readonly List<InvoiceItem> _items = [];

  public Guid Id { get; private set; }

  public Guid CustomerId { get; private set; }

  public DateOnly PeriodStart { get; private set; }

  public DateOnly PeriodEnd { get; private set; }

  public Money Total { get; private set; } = Money.Zero;

  public InvoiceStatus Status { get; private set; }

  public DateTimeOffset CreatedAt { get; private set; }

  public Customer Customer { get; private set; } = null!;

  public IReadOnlyCollection<InvoiceItem> Items => _items.AsReadOnly();

  // Construtor utilizado pelo Entity Framework Core.
  private Invoice()
  {
  }

  public Invoice(
      Guid id,
      Guid customerId,
      DateOnly periodStart,
      DateOnly periodEnd,
      DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
      throw new ArgumentException(
          "O ID da fatura é obrigatório.", nameof(id));

    if (customerId == Guid.Empty)
      throw new ArgumentException(
          "O ID do cliente é obrigatório.", nameof(customerId));

    if (periodStart >= periodEnd)
      throw new ArgumentException(
          "O início do período deve ser anterior ao fim do período.");

    Id = id;
    CustomerId = customerId;
    PeriodStart = periodStart;
    PeriodEnd = periodEnd;
    Total = Money.Zero;
    Status = InvoiceStatus.Open;
    CreatedAt = createdAt;
  }

  public void AddItem(InvoiceItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (Status != InvoiceStatus.Open)
      throw new InvalidOperationException(
          "Só é possível adicionar itens a uma fatura aberta.");

    if (item.InvoiceId != Id)
      throw new InvalidOperationException(
          "O item pertence a outra fatura.");

    if (item.Amount.IsZero)
      throw new InvalidOperationException(
          "O valor do item deve ser maior que zero.");

    if (_items.Any(existing =>
            existing.OperationId == item.OperationId))
    {
      throw new InvalidOperationException(
          "Esta operação já foi adicionada à fatura.");
    }

    _items.Add(item);
    RecalculateTotal();
  }

  public void FinalizeInvoice()
  {
    if (Status != InvoiceStatus.Open)
      throw new InvalidOperationException(
          "Somente uma fatura aberta pode ser finalizada.");

    if (_items.Count == 0)
      throw new InvalidOperationException(
          "Não é possível finalizar uma fatura sem itens.");

    if (Total.IsZero)
      throw new InvalidOperationException(
          "O total da fatura deve ser maior que zero.");

    Status = InvoiceStatus.PaymentPending;
  }

  public void StartPayment()
  {
    if (Status != InvoiceStatus.PaymentPending)
      throw new InvalidOperationException(
          "Somente uma fatura pendente pode iniciar o pagamento.");

    // O pagamento está sendo processado, mas o resultado
    // ainda não foi confirmado pelo provedor.
    Status = InvoiceStatus.PaymentUnknown;
  }

  public void MarkPaid()
  {
    if (Status is not InvoiceStatus.PaymentPending
        and not InvoiceStatus.PaymentUnknown)
    {
      throw new InvalidOperationException(
          "A fatura não pode ser marcada como paga a partir do estado atual.");
    }

    Status = InvoiceStatus.Paid;
  }

  public void MarkPaymentFailed()
  {
    if (Status is not InvoiceStatus.PaymentPending
        and not InvoiceStatus.PaymentUnknown)
    {
      throw new InvalidOperationException(
          "A fatura não pode ser marcada como falha a partir do estado atual.");
    }

    Status = InvoiceStatus.PaymentFailed;
  }

  public void RequireReconciliation()
  {
    if (Status != InvoiceStatus.PaymentUnknown)
    {
      throw new InvalidOperationException(
          "Somente pagamentos com resultado desconhecido exigem reconciliação.");
    }

    // Mantém PaymentUnknown até que o resultado seja confirmado.
    // Não existe um estado ReconciliationRequired no enum atual.
  }

  private void RecalculateTotal()
  {
    var total = Money.Zero;

    foreach (var item in _items)
    {
      total = total.Add(item.Amount);
    }

    Total = total;
  }
}
