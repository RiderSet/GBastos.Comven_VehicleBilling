using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;

namespace Comven_VehicleBilling.UnitTests.Entities;

public sealed class OperationTests
{
    private static Operation CreateOperation() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            OperationType.VehicleConsultation,
            Money.Positive(125.50m),
            "hash-teste-123",
            DateTimeOffset.UtcNow);

    [Fact]
    public void Constructor_ShouldStartAsPending()
    {
        var operation = CreateOperation();

        Assert.Equal(OperationStatus.Pending, operation.Status);
        Assert.Null(operation.RefundedAt);
        Assert.Null(operation.InvoicedAt);
        Assert.Null(operation.InvoiceId);
    }

    [Fact]
    public void MarkCharged_WhenPending_ShouldSetChargedStatus()
    {
        var operation = CreateOperation();

        operation.MarkCharged();

        Assert.Equal(OperationStatus.Charged, operation.Status);
    }

    [Fact]
    public void MarkAwaitingInvoice_WhenPending_ShouldSetAwaitingInvoiceStatus()
    {
        var operation = CreateOperation();

        operation.MarkAwaitingInvoice();

        Assert.Equal(OperationStatus.AwaitingInvoice, operation.Status);
    }

    [Fact]
    public void MarkFailed_WhenPending_ShouldSetFailedStatus()
    {
        var operation = CreateOperation();

        operation.MarkFailed();

        Assert.Equal(OperationStatus.Failed, operation.Status);
    }

    [Fact]
    public void MarkRefunded_WhenCharged_ShouldSetRefundedStatusAndDate()
    {
        var operation = CreateOperation();
        operation.MarkCharged();
        var refundedAt = DateTimeOffset.UtcNow;

        operation.MarkRefunded(refundedAt);

        Assert.Equal(OperationStatus.Refunded, operation.Status);
        Assert.Equal(refundedAt, operation.RefundedAt);
    }

    [Fact]
    public void MarkRefunded_WhenPending_ShouldThrow()
    {
        var operation = CreateOperation();

        Assert.Throws<InvalidOperationException>(
            () => operation.MarkRefunded(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkInvoiced_WhenAwaitingInvoice_ShouldSetInvoiceAndDate()
    {
        var operation = CreateOperation();
        operation.MarkAwaitingInvoice();
        var invoiceId = Guid.NewGuid();
        var invoicedAt = DateTimeOffset.UtcNow;

        operation.MarkInvoiced(invoiceId, invoicedAt);

        Assert.Equal(OperationStatus.Invoiced, operation.Status);
        Assert.Equal(invoiceId, operation.InvoiceId);
        Assert.Equal(invoicedAt, operation.InvoicedAt);
    }

    [Fact]
    public void MarkInvoiced_WhenPending_ShouldThrow()
    {
        var operation = CreateOperation();

        Assert.Throws<InvalidOperationException>(
            () => operation.MarkInvoiced(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkInvoiced_WithEmptyInvoiceId_ShouldThrow()
    {
        var operation = CreateOperation();
        operation.MarkAwaitingInvoice();

        Assert.Throws<ArgumentException>(
            () => operation.MarkInvoiced(
                Guid.Empty,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkCharged_WhenAlreadyCharged_ShouldThrow()
    {
        var operation = CreateOperation();
        operation.MarkCharged();

        Assert.Throws<InvalidOperationException>(
            operation.MarkCharged);
    }
}
