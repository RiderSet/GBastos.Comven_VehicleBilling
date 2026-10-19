using Comven_VehicleBilling.Application.Invoices;

namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface IInvoiceRepository
{
  Task<InvoiceDto> CreateAsync(
      CreateInvoiceCommand command,
      CancellationToken cancellationToken = default);

  Task<InvoiceDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default);

  Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
      Guid? customerId = null,
      CancellationToken cancellationToken = default);
}
