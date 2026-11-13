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

    public Guid? OriginalInvoiceId { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public Money Total { get; private set; } = Money.Zero;

    public InvoiceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Customer Customer { get; private set; } = null!;

    public IReadOnlyCollection<InvoiceItem> Items
        => _items.AsReadOnly();

    public bool IsSupplementary => OriginalInvoiceId.HasValue;

    private Invoice()
    {
    }

    public Invoice(
        Guid id,
        Guid customerId,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTimeOffset createdAt,
        Guid? originalInvoiceId = null,
        string? idempotencyKey = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Invoice id cannot be empty.",
                nameof(id));

        if (customerId == Guid.Empty)
            throw new ArgumentException(
                "Customer id cannot be empty.",
                nameof(customerId));

        if (periodEnd <= periodStart)
            throw new ArgumentException(
                "Invoice period end must be greater than period start.",
                nameof(periodEnd));

        if (originalInvoiceId == Guid.Empty)
            throw new ArgumentException(
                "Original invoice id cannot be empty.",
                nameof(originalInvoiceId));

        if (originalInvoiceId == id)
            throw new ArgumentException(
                "An invoice cannot reference itself as the original invoice.",
                nameof(originalInvoiceId));

        if (originalInvoiceId.HasValue &&
            string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException(
                "Supplementary invoices require an idempotency key.",
                nameof(idempotencyKey));

        if (idempotencyKey?.Length > 200)
            throw new ArgumentException(
                "Idempotency key cannot exceed 200 characters.",
                nameof(idempotencyKey));

        Id = id;
        CustomerId = customerId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        OriginalInvoiceId = originalInvoiceId;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();
        Total = Money.Zero;
        Status = InvoiceStatus.Open;
        CreatedAt = createdAt;
    }

    public void AddItem(InvoiceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status != InvoiceStatus.Open)
            throw new InvalidOperationException(
                "Items can only be added to an open invoice.");

        if (item.InvoiceId != Id)
            throw new InvalidOperationException(
                "Invoice item does not belong to this invoice.");

        if (item.Amount.Amount <= 0)
            throw new InvalidOperationException(
                "Invoice item amount must be greater than zero.");

        if (_items.Any(x => x.OperationId == item.OperationId))
            throw new InvalidOperationException(
                "The operation is already included in this invoice.");

        _items.Add(item);

        RecalculateTotal();
    }

    public void FinalizeInvoice()
    {
        if (Status != InvoiceStatus.Open)
            throw new InvalidOperationException(
                "Only open invoices can be finalized.");

        if (_items.Count == 0)
            throw new InvalidOperationException(
                "An invoice must contain at least one item.");

        if (Total.Amount <= 0)
            throw new InvalidOperationException(
                "Invoice total must be greater than zero.");

        Status = InvoiceStatus.PaymentPending;
    }

    public void StartPayment()
    {
        if (Status != InvoiceStatus.PaymentPending)
            throw new InvalidOperationException(
                "Only invoices pending payment can start payment.");

        Status = InvoiceStatus.PaymentProcessing;
    }

    public void MarkPaid()
    {
        if (Status != InvoiceStatus.PaymentProcessing
            && Status != InvoiceStatus.PaymentUnknown
            && Status != InvoiceStatus.PaymentPending)
        {
            throw new InvalidOperationException(
                "Invoice cannot be marked as paid from its current status.");
        }

        Status = InvoiceStatus.Paid;
    }

    public void MarkPaymentFailed()
    {
        if (Status != InvoiceStatus.PaymentProcessing
            && Status != InvoiceStatus.PaymentUnknown
            && Status != InvoiceStatus.PaymentPending)
        {
            throw new InvalidOperationException(
                "Invoice payment cannot be marked as failed from its current status.");
        }

        Status = InvoiceStatus.PaymentFailed;
    }

    public void RequireReconciliation()
    {
        if (Status != InvoiceStatus.PaymentUnknown)
            throw new InvalidOperationException(
                "Reconciliation is only required for invoices with unknown payment status.");
    }

    private void RecalculateTotal()
    {
        var total = Money.Zero;

        foreach (var item in _items)
            total = total.Add(item.Amount);

        Total = total;
    }
}