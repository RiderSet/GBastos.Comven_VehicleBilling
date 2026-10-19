using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Customers;

public sealed class CustomerService
{
  private readonly ICustomerRepository _repository;

  public CustomerService(ICustomerRepository repository)
  {
    _repository = repository;
  }

  public async Task<CustomerDto> CreateAsync(
      CreateCustomerCommand command,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(command.Name))
      throw new ApplicationException("O nome é obrigatório.");

    if (string.IsNullOrWhiteSpace(command.Document))
      throw new ApplicationException("O documento é obrigatório.");

    if (string.IsNullOrWhiteSpace(command.Email))
      throw new ApplicationException("O e-mail é obrigatório.");

    return await _repository.CreateAsync(
        command, cancellationToken);
  }

  public async Task<CustomerDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  {
    if (id == Guid.Empty)
      throw new ApplicationException("O identificador do cliente é inválido.");

    return await _repository.GetByIdAsync(id, cancellationToken);
  }

  public Task<IReadOnlyList<CustomerDto>> GetAllAsync(
      CancellationToken cancellationToken = default)
  {
    return _repository.GetAllAsync(cancellationToken);
  }
}
