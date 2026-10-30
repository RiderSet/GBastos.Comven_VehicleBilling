using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Operations;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

public sealed class OperationRepository : IOperationRepository
{
  private readonly VehicleBillingDbContext _dbContext;

  public OperationRepository(VehicleBillingDbContext dbContext)
  {
    _dbContext = dbContext
        ?? throw new ArgumentNullException(nameof(dbContext));
  }

  public async Task<OperationDto> CreateAsync(
      CreateOperationCommand command,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (command.ExternalOperationId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador externo da operação é obrigatório.");
    }

    if (command.CustomerId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador do cliente é obrigatório.");
    }

    if (!Enum.TryParse<OperationType>(
            command.OperationType,
            true,
            out var operationType)
        || !Enum.IsDefined(operationType))
    {
      throw new ArgumentException(
          "O tipo de operação informado é inválido.");
    }

    var amount = Money.Positive(command.Amount);

    var customerExists = await _dbContext.Customers
        .AnyAsync(
            customer => customer.Id == command.CustomerId,
            cancellationToken);

    if (!customerExists)
    {
      throw new InvalidOperationException(
          "O cliente informado não foi encontrado.");
    }

    var existing = await _dbContext.Operations
        .AsNoTracking()
        .FirstOrDefaultAsync(
            operation =>
                operation.ExternalOperationId ==
                command.ExternalOperationId,
            cancellationToken);

    if (existing is not null)
    {
      var sameRequest =
          existing.CustomerId == command.CustomerId
          && existing.Type == operationType
          && existing.Amount == amount;

      if (!sameRequest)
      {
        throw new InvalidOperationException(
            "O identificador externo já foi utilizado com dados diferentes.");
      }

      return Map(existing);
    }

    var canonicalRequest = string.Join(
        "|",
        command.CustomerId.ToString("N"),
        command.ExternalOperationId.ToString("N"),
        operationType.ToString(),
        command.Amount.ToString(
            "0.00",
            CultureInfo.InvariantCulture));

    var requestHash = Convert.ToHexString(
        SHA256.HashData(
            Encoding.UTF8.GetBytes(canonicalRequest)));

    var operation = new Operation(
        Guid.NewGuid(),
        command.ExternalOperationId,
        command.CustomerId,
        operationType,
        amount,
        requestHash,
        DateTimeOffset.UtcNow);

    await _dbContext.Operations.AddAsync(
        operation,
        cancellationToken);

    try
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
      return Map(operation);
    }
    catch (DbUpdateException exception)
        when (IsExternalOperationIdUniqueViolation(exception))
    {
      // Outra requisição gravou o mesmo identificador primeiro.
      _dbContext.Entry(operation).State = EntityState.Detached;

      var concurrentOperation = await _dbContext.Operations
          .AsNoTracking()
          .FirstOrDefaultAsync(
              item => item.ExternalOperationId ==
                      command.ExternalOperationId,
              cancellationToken);

      // Se não encontrarmos a operação concorrente,
      // preservamos a exceção original.
      if (concurrentOperation is null)
      {
        throw;
      }

      var sameRequest =
          concurrentOperation.CustomerId == command.CustomerId
          && concurrentOperation.Type == operationType
          && concurrentOperation.Amount == amount;

      if (!sameRequest)
      {
        throw new InvalidOperationException(
            "O identificador externo já foi utilizado com dados diferentes.",
            exception);
      }

      return Map(concurrentOperation);
    }
  }

  public async Task<OperationDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  {
    var operation = await _dbContext.Operations
        .AsNoTracking()
        .FirstOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

    return operation is null ? null : Map(operation);
  }

  public async Task<IReadOnlyList<OperationDto>> GetAllAsync(
      Guid? customerId = null,
      CancellationToken cancellationToken = default)
  {
    var query = _dbContext.Operations
        .AsNoTracking()
        .AsQueryable();

    if (customerId.HasValue)
    {
      query = query.Where(
          operation => operation.CustomerId == customerId.Value);
    }

    var operations = await query
        .OrderByDescending(operation => operation.CreatedAt)
        .ToListAsync(cancellationToken);

    return operations.Select(Map).ToList();
  }

  private static bool IsExternalOperationIdUniqueViolation(
    DbUpdateException exception)
  {
    for (Exception? current = exception;
         current is not null;
         current = current.InnerException)
    {
      if (current is not PostgresException postgresException)
        continue;

      if (postgresException.SqlState != PostgresErrorCodes.UniqueViolation)
        continue;

      var constraintName = postgresException.ConstraintName;

      if (string.Equals(
              constraintName,
              "ux_operations_external_operation_id",
              StringComparison.Ordinal)
          || string.Equals(
              constraintName,
              "ux_operations_customer_request_hash",
              StringComparison.Ordinal))
      {
        return true;
      }
    }

    return false;
  }

  private static OperationDto Map(Operation operation)
      => new(
          operation.Id,
          operation.CustomerId,
          operation.Type.ToString(),
          operation.Amount.Amount,
          operation.CreatedAt.UtcDateTime);
}
