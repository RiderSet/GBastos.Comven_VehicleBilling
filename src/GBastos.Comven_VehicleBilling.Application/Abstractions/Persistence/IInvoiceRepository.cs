using Comven_VehicleBilling.Application.Invoices;
using Comven_VehicleBilling.Application.Abstractions.Payments;

namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface IInvoiceRepository
{
    Task<InvoiceDto> CreateAsync(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken = default);

    Task<InvoiceDto> CreateSupplementaryAsync(
        CreateSupplementaryInvoiceCommand command,
        CancellationToken cancellationToken = default);

    Task<InvoiceDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
        Guid? customerId = null,
        CancellationToken cancellationToken = default);

    Task<InvoiceDto?> StartPaymentAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceDto> CompletePaymentAsync(
        Guid invoiceId,
        PaymentOutcome outcome,
        CancellationToken cancellationToken = default);
}
