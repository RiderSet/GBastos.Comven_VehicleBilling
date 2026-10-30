using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Customers;

public sealed class CustomerService
{
    private readonly ICustomerRepository _repository;

    public CustomerService(ICustomerRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ArgumentException(
                "O nome e obrigatorio.",
                nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Document))
        {
            throw new ArgumentException(
                "O documento e obrigatorio.",
                nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            throw new ArgumentException(
                "O e-mail e obrigatorio.",
                nameof(command));
        }

        return await _repository.CreateAsync(
            command,
            cancellationToken);
    }

    public async Task<CustomerDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do cliente e invalido.",
                nameof(id));
        }

        return await _repository.GetByIdAsync(
            id,
            cancellationToken);
    }

    public Task<IReadOnlyList<CustomerDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }
}