using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class InvoiceItem
{
    public Guid InvoiceId { get; private set; }

    public Guid OperationId { get; private set; }

    public Money Amount { get; private set; } = Money.Zero;

    public Invoice Invoice { get; private set; } = null!;

    private InvoiceItem()
    {
    }

    public InvoiceItem(
        Guid invoiceId,
        Guid operationId,
        Money amount)
    {
        if (invoiceId == Guid.Empty)
            throw new ArgumentException(
                "InvoiceId cannot be empty.",
                nameof(invoiceId));

        if (operationId == Guid.Empty)
            throw new ArgumentException(
                "OperationId cannot be empty.",
                nameof(operationId));

        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Invoice item amount must be greater than zero.");

        InvoiceId = invoiceId;
        OperationId = operationId;
        Amount = amount;
    }
}
