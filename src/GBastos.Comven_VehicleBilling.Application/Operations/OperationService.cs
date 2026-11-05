using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Domain.Enums;

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
          "O identificador externo da operaÃ§Ã£o Ã© obrigatÃ³rio.",
          nameof(command));
    }

    if (command.CustomerId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador do cliente Ã© obrigatÃ³rio.",
          nameof(command));
    }

    if (string.IsNullOrWhiteSpace(command.OperationType))
    {
      throw new ArgumentException(
          "O tipo da operaÃ§Ã£o Ã© obrigatÃ³rio.",
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

  public async Task<OperationDetailDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (id == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador da operaÃ§Ã£o Ã© obrigatÃ³rio.",
          nameof(id));
    }

    return await _repository.GetByIdAsync(
        id,
        cancellationToken);
  }

  public Task<OperationPagedResult> GetAllAsync(
      Guid? customerId = null,
      string? operationType = null,
      DateTimeOffset? startDate = null,
      DateTimeOffset? endDate = null,
      int page = DefaultPage,
      int pageSize = DefaultPageSize,
      string? status = null,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (customerId.HasValue && customerId.Value == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador do cliente Ã© invÃ¡lido.",
          nameof(customerId));
    }

    if (page < 1)
    {
      throw new ArgumentOutOfRangeException(
          nameof(page),
          "A pÃ¡gina deve ser maior que zero.");
    }

    if (status is not null)
    {
      if (string.IsNullOrWhiteSpace(status)
          || !Enum.TryParse<OperationStatus>(
              status.Trim(),
              true,
              out var parsedStatus)
          || !Enum.IsDefined(parsedStatus))
      {
        throw new ArgumentException(
            "O status da operação informado é inválido.",
            nameof(status));
      }

      status = parsedStatus.ToString();
    }

    if (pageSize < 1 || pageSize > MaxPageSize)
    {
      throw new ArgumentOutOfRangeException(
          nameof(pageSize),
          $"O tamanho da pÃ¡gina deve estar entre 1 e {MaxPageSize}.");
    }

    if (page > int.MaxValue / pageSize)
    {
      throw new ArgumentOutOfRangeException(
          nameof(page),
          "O nÃºmero da pÃ¡gina Ã© muito grande.");
    }

    if (startDate.HasValue
        && endDate.HasValue
        && startDate.Value > endDate.Value)
    {
      throw new ArgumentException(
          "A data inicial nÃ£o pode ser posterior Ã  data final.");
    }

    if (operationType is not null
        && string.IsNullOrWhiteSpace(operationType))
    {
      throw new ArgumentException(
          "O tipo da operaÃ§Ã£o informado Ã© invÃ¡lido.",
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
        cancellationToken,
        status);
  }
}

