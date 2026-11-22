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

      // A fatura existe, mas não pode iniciar outro pagamento.
      return existingInvoice;
    }

    if (!string.Equals(
            invoice.Status,
            "PaymentProcessing",
            StringComparison.Ordinal))
    {
      return invoice;
    }

    var idempotencyKey = invoice.Id.ToString("N");

    var request = new PaymentRequest(
        invoice.Id,
        invoice.CustomerId,
        invoice.Amount,
        idempotencyKey);

    PaymentResult result;

    try
    {
      result = await _paymentProvider.ProcessPaymentAsync(
          request,
          cancellationToken);
    }
    catch
    {
      // Uma falha de comunicação pode ocorrer depois da cobrança.
      // Não presumimos que o pagamento foi rejeitado.
      await _repository.CompletePaymentAsync(
          invoice.Id,
          PaymentOutcome.Unknown,
          CancellationToken.None);

      throw;
    }

    return await _repository.CompletePaymentAsync(
        invoice.Id,
        result.Outcome,
        cancellationToken);
  }
}
