using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Operations;

public sealed class OperationService
{
    private readonly IOperationRepository _repository;

    public OperationService(IOperationRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<OperationDto> CreateAsync(
        CreateOperationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CustomerId == Guid.Empty)
            throw new ApplicationException("O cliente é obrigatório.");

        if (string.IsNullOrWhiteSpace(command.OperationType))
            throw new ApplicationException("O tipo da operação é obrigatório.");

        if (command.Amount <= 0)
            throw new ApplicationException("O valor deve ser maior que zero.");

        return await _repository.CreateAsync(command, cancellationToken);
    }

    public async Task<OperationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ApplicationException(
                "O identificador da operação é inválido.");

        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<OperationDto>> GetAllAsync(
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
            throw new ApplicationException(
                "O identificador do cliente é inválido.");

        return _repository.GetAllAsync(customerId, cancellationToken);
    }
}
