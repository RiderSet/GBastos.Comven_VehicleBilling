using Comven_VehicleBilling.Application.Operations;

namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface IOperationRepository
{
  Task<OperationDto> CreateAsync(
      CreateOperationCommand command,
      CancellationToken cancellationToken = default);

  Task<OperationDetailDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default);

  Task<RefundOperationResult> RefundAsync(
    Guid operationId,
    CancellationToken cancellationToken = default);

  Task<OperationPagedResult> GetAllAsync(
      Guid? customerId,
      string? operationType,
      DateTimeOffset? startDate,
      DateTimeOffset? endDate,
      int page,
      int pageSize,
      CancellationToken cancellationToken = default,
      string? status = null);
}
