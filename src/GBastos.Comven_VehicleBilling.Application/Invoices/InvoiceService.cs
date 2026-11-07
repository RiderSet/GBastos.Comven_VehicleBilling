using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Invoices;

public sealed class InvoiceService
{
    private readonly IInvoiceRepository _repository;

    public InvoiceService(IInvoiceRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<InvoiceDto> CreateAsync(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.CustomerId == Guid.Empty)
            throw new ArgumentException(
                "O identificador do cliente é inválido.",
                nameof(command));

        if (command.ReferenceDate == DateOnly.MinValue)
            throw new ArgumentException(
                "A data de referência é inválida.",
                nameof(command));

        return await _repository.CreateAsync(
            command, cancellationToken);
    }

    public async Task<InvoiceDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (id == Guid.Empty)
            throw new ArgumentException(
                "O identificador da fatura é inválido.",
                nameof(id));

        return await _repository.GetByIdAsync(
            id, cancellationToken);
    }

    public Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (customerId == Guid.Empty)
            throw new ArgumentException(
                "O identificador do cliente é inválido.",
                nameof(customerId));

        return _repository.GetAllAsync(
            customerId, cancellationToken);
    }
}
