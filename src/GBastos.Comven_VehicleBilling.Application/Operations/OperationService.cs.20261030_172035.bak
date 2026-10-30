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
        cancellationToken.ThrowIfCancellationRequested();

        if (command.ExternalOperationId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador externo da operacao e obrigatorio.",
                nameof(command));
        }

        if (command.CustomerId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do cliente e obrigatorio.",
                nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.OperationType))
        {
            throw new ArgumentException(
                "O tipo da operacao e obrigatorio.",
                nameof(command));
        }

        if (command.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "O valor deve ser maior que zero.");
        }

        return await _repository.CreateAsync(
            command,
            cancellationToken);
    }

    public async Task<OperationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da operacao e invalido.",
                nameof(id));
        }

        return await _repository.GetByIdAsync(
            id,
            cancellationToken);
    }

    public Task<IReadOnlyList<OperationDto>> GetAllAsync(
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do cliente e invalido.",
                nameof(customerId));
        }

        return _repository.GetAllAsync(
            customerId,
            cancellationToken);
    }
}