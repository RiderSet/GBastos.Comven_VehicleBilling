using Comven_VehicleBilling.Application.Abstractions.Payments;
using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Invoices;

public sealed class InvoicePaymentService
{
  private readonly IInvoiceRepository _repository;
  private readonly IPaymentProvider _paymentProvider;

  public InvoicePaymentService(
      IInvoiceRepository repository,
      IPaymentProvider paymentProvider)
  {
    _repository = repository
        ?? throw new ArgumentNullException(nameof(repository));

    _paymentProvider = paymentProvider
        ?? throw new ArgumentNullException(nameof(paymentProvider));
  }

  public async Task<InvoiceDto> PayAsync(
      Guid invoiceId,
      CancellationToken cancellationToken = default)
  {
    if (invoiceId == Guid.Empty)
      throw new ArgumentException(
          "O identificador da fatura é inválido.",
          nameof(invoiceId));

    var invoice = await _repository.StartPaymentAsync(
        invoiceId,
        cancellationToken);

    if (invoice is null)
    {
      var existingInvoice = await _repository.GetByIdAsync(
          invoiceId,
          cancellationToken);

      if (existingInvoice is null)
        throw new InvalidOperationException("Fatura não encontrada.");

      if (!RequiresReconciliation(existingInvoice.Status))
        return existingInvoice;

      return await ReconcilePaymentAsync(
          existingInvoice,
          cancellationToken);
    }

    if (!string.Equals(
            invoice.Status,
            "PaymentProcessing",
            StringComparison.Ordinal))
    {
      return invoice;
    }

    var request = CreatePaymentRequest(invoice);

    PaymentResult result;

    try
    {
      result = await _paymentProvider.ProcessPaymentAsync(
          request,
          cancellationToken);
    }
    catch
    {
      // Uma falha de comunicação não significa rejeição.
      // A cobrança pode ter sido realizada pelo provedor.
      await _repository.CompletePaymentAsync(
          invoice.Id,
          PaymentOutcome.Unknown,
          CancellationToken.None);

      throw;
    }

    return await _repository.CompletePaymentAsync(
        invoice.Id,
        result.Outcome,
        cancellationToken,
        result.ExternalPaymentId);
  }

  private async Task<InvoiceDto> ReconcilePaymentAsync(
      InvoiceDto invoice,
      CancellationToken cancellationToken)
  {
    var idempotencyKey = invoice.Id.ToString("N");

    // Consulta exclusivamente a cobrança original.
    // Nunca inicia uma segunda cobrança durante a reconciliação.
    var result = await _paymentProvider.GetPaymentAsync(
        idempotencyKey,
        cancellationToken);

    if (result is null)
      return invoice;

    return await _repository.CompletePaymentAsync(
        invoice.Id,
        result.Outcome,
        cancellationToken,
        result.ExternalPaymentId);
  }

  private static bool RequiresReconciliation(string status)
      => string.Equals(
             status,
             "PaymentUnknown",
             StringComparison.Ordinal)
         || string.Equals(
             status,
             "PaymentProcessing",
             StringComparison.Ordinal);

  private static PaymentRequest CreatePaymentRequest(
      InvoiceDto invoice)
  {
    var idempotencyKey = invoice.Id.ToString("N");

    return new PaymentRequest(
        invoice.Id,
        invoice.CustomerId,
        invoice.Amount,
        idempotencyKey);
  }
}
