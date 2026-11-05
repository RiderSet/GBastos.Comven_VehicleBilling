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

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("O nome é obrigatório.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Document))
            throw new ArgumentException("O documento é obrigatório.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("O e-mail é obrigatório.", nameof(command));

        return await _repository.CreateAsync(
            command,
            cancellationToken);
    }

    public async Task<CustomerDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "O identificador do cliente é inválido.",
                nameof(id));

        return await _repository.GetByIdAsync(
            id,
            cancellationToken);
    }

    public Task<IReadOnlyList<CustomerDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

  public async Task<CustomerBalanceDto?> GetBalanceAsync(
      Guid customerId,
      CancellationToken cancellationToken = default)
  {
    if (customerId == Guid.Empty)
      throw new ArgumentException(
          "O identificador do cliente é inválido.",
          nameof(customerId));

    return await _repository.GetBalanceAsync(
        customerId,
        cancellationToken);
  }

}
