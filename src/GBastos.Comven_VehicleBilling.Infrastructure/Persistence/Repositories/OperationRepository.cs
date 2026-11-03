using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Operations;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Npgsql;
using Comven_VehicleBilling.Infrastructure.Outbox;

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

    try
    {
      await using var transaction =
          await _dbContext.Database.BeginTransactionAsync(
              cancellationToken);

      // Serializa operações concorrentes do mesmo cliente.
      var customer = await _dbContext.Customers
          .FromSqlInterpolated(
              $"SELECT * FROM customers WHERE id = {command.CustomerId} FOR UPDATE")
          .FirstOrDefaultAsync(cancellationToken);

      if (customer is null)
      {
        throw new InvalidOperationException(
            "O cliente informado não foi encontrado.");
      }

      // Verifica a idempotência dentro da transação.
      var existing = await _dbContext.Operations
          .AsNoTracking()
          .FirstOrDefaultAsync(
              operation =>
                  operation.ExternalOperationId ==
                  command.ExternalOperationId,
              cancellationToken);

      if (existing is not null)
      {
        EnsureSameRequest(
            existing,
            command.CustomerId,
            operationType,
            amount);

        await transaction.CommitAsync(cancellationToken);

        return Map(existing);
      }

      var operation = new Operation(
          Guid.NewGuid(),
          command.ExternalOperationId,
          command.CustomerId,
          operationType,
          amount,
          requestHash,
          DateTimeOffset.UtcNow);

      if (customer.Plan == CustomerPlan.Prepaid)
      {
        // O débito será confirmado junto com a operação,
        // o lançamento financeiro e a mensagem da Outbox.
        customer.Debit(amount);

        operation.MarkCharged();

        var ledgerEntry = new LedgerEntry(
            Guid.NewGuid(),
            customer.Id,
            operation.Id,
            amount,
            LedgerEntryType.Debit,
            $"Cobrança da operação {operationType}",
            DateTimeOffset.UtcNow);

        await _dbContext.LedgerEntries.AddAsync(
            ledgerEntry,
            cancellationToken);
      }
      else if (customer.Plan == CustomerPlan.Postpaid)
      {
        // A operação ficará disponível para o faturamento mensal.
        operation.MarkAwaitingInvoice();
      }
      else
      {
        throw new InvalidOperationException(
            "O plano do cliente é inválido.");
      }

      await _dbContext.Operations.AddAsync(
          operation,
          cancellationToken);

      // Registra o evento na mesma transação da operação.
      var eventPayload = JsonSerializer.Serialize(new
      {
        EventId = Guid.NewGuid(),
        OperationId = operation.Id,
        ExternalOperationId = command.ExternalOperationId,
        CustomerId = command.CustomerId,
        OperationType = operationType.ToString(),
        Amount = amount.Amount,
        CustomerPlan = customer.Plan.ToString(),
        CreatedAt = operation.CreatedAt.UtcDateTime
      });

      var outboxMessage = new OutboxMessage(
          "OperationCreated.v1",
          operation.Id,
          eventPayload);

      await _dbContext.OutboxMessages.AddAsync(
          outboxMessage,
          cancellationToken);

      // Persiste operação, lançamento e Outbox atomicamente.
      await _dbContext.SaveChangesAsync(cancellationToken);

      await transaction.CommitAsync(cancellationToken);

      return Map(operation);
    }
    catch (DbUpdateException exception)
        when (IsOperationUniqueViolation(exception))
    {
      // Descarta o estado local da transação que falhou.
      _dbContext.ChangeTracker.Clear();

      // Uma violação do ID externo pode representar uma
      // requisição concorrente já confirmada.
      var concurrentOperation = await _dbContext.Operations
          .AsNoTracking()
          .FirstOrDefaultAsync(
              item => item.ExternalOperationId ==
                      command.ExternalOperationId,
              cancellationToken);

      if (concurrentOperation is not null)
      {
        EnsureSameRequest(
            concurrentOperation,
            command.CustomerId,
            operationType,
            amount);

        return Map(concurrentOperation);
      }

      // Uma violação do hash sem operação encontrada pelo ID
      // externo indica reutilização de uma chave de requisição.
      if (IsRequestHashUniqueViolation(exception))
      {
        throw new InvalidOperationException(
            "Já existe uma operação registrada com os mesmos dados da requisição.",
            exception);
      }

      // Não transforma outras violações de integridade
      // em respostas idempotentes.
      throw;
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

    return operation is null
        ? null
        : Map(operation);
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
          operation =>
              operation.CustomerId == customerId.Value);
    }

    var operations = await query
        .OrderByDescending(operation => operation.CreatedAt)
        .ToListAsync(cancellationToken);

    return operations
        .Select(Map)
        .ToList();
  }

  private static void EnsureSameRequest(
      Operation operation,
      Guid customerId,
      OperationType operationType,
      Money amount)
  {
    var sameRequest =
        operation.CustomerId == customerId
        && operation.Type == operationType
        && operation.Amount == amount;

    if (!sameRequest)
    {
      throw new InvalidOperationException(
          "O identificador externo já foi utilizado com dados diferentes.");
    }
  }

  private static bool IsOperationUniqueViolation(
      DbUpdateException exception)
  {
    for (Exception? current = exception;
         current is not null;
         current = current.InnerException)
    {
      if (current is not PostgresException postgresException)
      {
        continue;
      }

      if (postgresException.SqlState !=
          PostgresErrorCodes.UniqueViolation)
      {
        continue;
      }

      return string.Equals(
                 postgresException.ConstraintName,
                 "ux_operations_external_operation_id",
                 StringComparison.Ordinal)
             || IsRequestHashUniqueViolation(exception);
    }

    return false;
  }

  private static bool IsRequestHashUniqueViolation(
      DbUpdateException exception)
  {
    for (Exception? current = exception;
         current is not null;
         current = current.InnerException)
    {
      if (current is PostgresException postgresException
          && postgresException.SqlState ==
             PostgresErrorCodes.UniqueViolation)
      {
        return string.Equals(
            postgresException.ConstraintName,
            "ux_operations_customer_request_hash",
            StringComparison.Ordinal);
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
