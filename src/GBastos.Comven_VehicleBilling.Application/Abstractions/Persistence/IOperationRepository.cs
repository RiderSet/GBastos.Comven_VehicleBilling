using Comven_VehicleBilling.Application.Operations;

namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface IOperationRepository
{
    Task<OperationDto> CreateAsync(
        CreateOperationCommand command,
        CancellationToken cancellationToken = default);

    Task<OperationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationDto>> GetAllAsync(
        Guid? customerId = null,
        CancellationToken cancellationToken = default);
}
