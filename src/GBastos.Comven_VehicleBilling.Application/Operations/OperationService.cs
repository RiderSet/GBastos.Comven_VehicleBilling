using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Domain.Enums;
using FluentValidation;

namespace Comven_VehicleBilling.Application.Operations;

public sealed class OperationService
{
  private const int DefaultPage = 1;
  private const int DefaultPageSize = 50;
  private const int MaxPageSize = 100;

  private readonly IValidator<CreateOperationCommand> _validator;
  private readonly IOperationRepository _repository;

  public OperationService(IOperationRepository repository)
      : this(repository, new CreateOperationCommandValidator())
  {
  }

  public OperationService(
      IOperationRepository repository,
      IValidator<CreateOperationCommand> validator)
  {
    _repository = repository
        ?? throw new ArgumentNullException(nameof(repository));

    _validator = validator
        ?? throw new ArgumentNullException(nameof(validator));
  }

  public async Task<OperationDto> CreateAsync(
      CreateOperationCommand command,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    cancellationToken.ThrowIfCancellationRequested();

    var validationResult = await _validator.ValidateAsync(
        command,
        cancellationToken);

    if (!validationResult.IsValid)
    {
      var errors = string.Join(
          Environment.NewLine,
          validationResult.Errors.Select(error => error.ErrorMessage));

      throw new ArgumentException(errors, nameof(command));
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
          "O identificador da operação é obrigatório.",
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

    if (operationType is not null)
    {
      if (string.IsNullOrWhiteSpace(operationType)
          || !Enum.TryParse<OperationType>(
              operationType.Trim(),
              true,
              out var parsedType)
          || !Enum.IsDefined(parsedType))
      {
        throw new ArgumentException(
            "O tipo da operação informado é inválido.",
            nameof(operationType));
      }

      operationType = parsedType.ToString();
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

  public async Task<RefundOperationResult> RefundAsync(
      Guid operationId,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (operationId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador da operação é obrigatório.",
          nameof(operationId));
    }

    return await _repository.RefundAsync(
        operationId,
        cancellationToken);
  }

  public sealed class CreateOperationCommandValidator
    : AbstractValidator<CreateOperationCommand>
  {
    public CreateOperationCommandValidator()
    {
      RuleFor(x => x.ExternalOperationId)
          .NotEmpty()
          .WithMessage(
              "O identificador externo da operação é obrigatório.");

      RuleFor(x => x.CustomerId)
          .NotEmpty()
          .WithMessage(
              "O identificador do cliente é obrigatório.");

      RuleFor(x => x.OperationType)
          .NotEmpty()
          .WithMessage(
              "O tipo da operação é obrigatório.")
          .MaximumLength(50)
          .WithMessage(
              "O tipo da operação deve ter até 50 caracteres.");

      RuleFor(x => x.Amount)
          .GreaterThan(0)
          .WithMessage(
              "O valor da operação deve ser maior que zero.")
          .Must(amount => decimal.Round(amount, 2) == amount)
          .WithMessage(
              "O valor da operação deve ter no máximo duas casas decimais.")
          .LessThanOrEqualTo(9999999999999999.99m)
          .WithMessage(
              "O valor excede o limite permitido.");
    }
  }
}
