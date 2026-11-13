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
                "O identificador do cliente Ã© invÃ¡lido.",
                nameof(command));

        if (command.ReferenceDate == DateOnly.MinValue)
            throw new ArgumentException(
                "A data de referÃªncia Ã© invÃ¡lida.",
                nameof(command));

        return await _repository.CreateAsync(
            command, cancellationToken);
    }

    public async Task<InvoiceDto> CreateSupplementaryAsync(
        CreateSupplementaryInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.CustomerId == Guid.Empty)
            throw new ArgumentException(
                "O identificador do cliente é inválido.",
                nameof(command));

        if (command.OriginalInvoiceId == Guid.Empty)
            throw new ArgumentException(
                "O identificador da fatura original é inválido.",
                nameof(command));

        ArgumentException.ThrowIfNullOrWhiteSpace(
            command.IdempotencyKey);

        if (command.IdempotencyKey.Trim().Length > 200)
            throw new ArgumentException(
                "A chave de idempotência deve ter no máximo 200 caracteres.",
                nameof(command));

        return await _repository.CreateSupplementaryAsync(
            command,
            cancellationToken);
    }

  public async Task<InvoiceDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (id == Guid.Empty)
      throw new ArgumentException(
          "O identificador da fatura Ã© invÃ¡lido.",
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
                "O identificador do cliente Ã© invÃ¡lido.",
                nameof(customerId));

        return _repository.GetAllAsync(
            customerId, cancellationToken);
    }
}
