using Comven_VehicleBilling.Application.Customers;

namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
  Task<CustomerDto> CreateAsync(
      CreateCustomerCommand command,
      CancellationToken cancellationToken = default);

  Task<CustomerDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default);

  Task<IReadOnlyList<CustomerDto>> GetAllAsync(
      CancellationToken cancellationToken = default);
}
