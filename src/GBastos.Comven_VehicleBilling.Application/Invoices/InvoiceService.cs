using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Invoices;

public sealed class InvoiceService
{
  private readonly IInvoiceRepository _repository;

  public InvoiceService(IInvoiceRepository repository)
  {
    _repository = repository;
  }

  public async Task<InvoiceDto> CreateAsync(
      CreateInvoiceCommand command,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (command.CustomerId == Guid.Empty)
      throw new ApplicationException("O cliente é obrigatório.");

    return await _repository.CreateAsync(
        command, cancellationToken);
  }

  public async Task<InvoiceDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  {
    if (id == Guid.Empty)
      throw new ApplicationException("O identificador da fatura é inválido.");

    return await _repository.GetByIdAsync(id, cancellationToken);
  }

  public Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
      Guid? customerId = null,
      CancellationToken cancellationToken = default)
  {
    if (customerId == Guid.Empty)
      throw new ApplicationException("O identificador do cliente é inválido.");

    return _repository.GetAllAsync(customerId, cancellationToken);
  }
}
