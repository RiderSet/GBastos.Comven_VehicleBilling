using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Operations;

public sealed class OperationService
{
  private const int DefaultPage = 1;
  private const int DefaultPageSize = 50;
  private const int MaxPageSize = 100;

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
          "O identificador externo da operação é obrigatório.",
          nameof(command));
    }

    if (command.CustomerId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador do cliente é obrigatório.",
          nameof(command));
    }

    if (string.IsNullOrWhiteSpace(command.OperationType))
    {
      throw new ArgumentException(
          "O tipo da operação é obrigatório.",
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
    cancellationToken.ThrowIfCancellationRequested();

    if (id == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador da operação é obrigatório.",
          nameof(id));
    }

    return await _repository.GetByIdAsync(
        id,
        cancellationToken);
  }

  public Task<IReadOnlyList<OperationDto>> GetAllAsync(
      Guid? customerId = null,
      string? operationType = null,
      DateTimeOffset? startDate = null,
      DateTimeOffset? endDate = null,
      int page = DefaultPage,
      int pageSize = DefaultPageSize,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (customerId.HasValue && customerId.Value == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador do cliente é inválido.",
          nameof(customerId));
    }

    if (page < 1)
    {
      throw new ArgumentOutOfRangeException(
          nameof(page),
          "A página deve ser maior que zero.");
    }

    if (pageSize < 1 || pageSize > MaxPageSize)
    {
      throw new ArgumentOutOfRangeException(
          nameof(pageSize),
          $"O tamanho da página deve estar entre 1 e {MaxPageSize}.");
    }

    if (page > int.MaxValue / pageSize)
    {
      throw new ArgumentOutOfRangeException(
          nameof(page),
          "O número da página é muito grande.");
    }

    if (startDate.HasValue
        && endDate.HasValue
        && startDate.Value > endDate.Value)
    {
      throw new ArgumentException(
          "A data inicial não pode ser posterior à data final.");
    }

    if (operationType is not null
        && string.IsNullOrWhiteSpace(operationType))
    {
      throw new ArgumentException(
          "O tipo da operação informado é inválido.",
          nameof(operationType));
    }

    operationType = operationType?.Trim();

    return _repository.GetAllAsync(
        customerId,
        operationType,
        startDate,
        endDate,
        page,
        pageSize,
        cancellationToken);
  }
}
