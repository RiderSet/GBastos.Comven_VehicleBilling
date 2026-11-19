using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Operations;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Outbox;
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

  public async Task<RefundOperationResult> RefundAsync(
      Guid operationId,
      CancellationToken cancellationToken = default)
  {
    if (operationId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador da operaÃ§Ã£o Ã© obrigatÃ³rio.",
          nameof(operationId));
    }

    await using var transaction =
        await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

    // Primeiro bloqueia o cliente.
    // O fluxo de criaÃ§Ã£o tambÃ©m bloqueia o cliente,
    // portanto ambos seguem a mesma ordem de bloqueio.
    var operationForCustomer = await _dbContext.Operations
        .AsNoTracking()
        .Where(operation => operation.Id == operationId)
        .Select(operation => new
        {
          operation.Id,
          operation.CustomerId
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (operationForCustomer is null)
    {
      throw new KeyNotFoundException(
          "A operaÃ§Ã£o informada nÃ£o foi encontrada.");
    }

    var customer = await _dbContext.Customers
        .FromSqlInterpolated(
            $"SELECT * FROM customers WHERE id = {operationForCustomer.CustomerId} FOR UPDATE")
        .SingleOrDefaultAsync(cancellationToken);

    if (customer is null)
    {
      throw new InvalidOperationException(
          "O cliente da operaÃ§Ã£o nÃ£o foi encontrado.");
    }

    // Agora bloqueia a operaÃ§Ã£o dentro da mesma transaÃ§Ã£o.
    var operation = await _dbContext.Operations
        .FromSqlInterpolated(
            $"SELECT * FROM operations WHERE id = {operationId} FOR UPDATE")
        .SingleOrDefaultAsync(cancellationToken);

    if (operation is null)
    {
      throw new KeyNotFoundException(
          "A operaÃ§Ã£o informada nÃ£o foi encontrada.");
    }

    // IdempotÃªncia do estorno.
    // Uma segunda chamada nÃ£o devolve dinheiro novamente.
    if (operation.Status == OperationStatus.Refunded)
    {
      var alreadyRefundedDetail =
          await BuildOperationDetailAsync(
              operation,
              cancellationToken);

      await transaction.CommitAsync(cancellationToken);

      return new RefundOperationResult(
          alreadyRefundedDetail,
          true);
    }

    if (operation.Status != OperationStatus.Charged)
    {
      throw new InvalidOperationException(
          "Somente operaÃ§Ãµes cobradas podem ser reembolsadas.");
    }

    if (customer.Plan != CustomerPlan.Prepaid)
    {
      throw new InvalidOperationException(
          "Somente operaÃ§Ãµes prÃ©-pagas podem ser reembolsadas.");
    }

    var utcNow = DateTime.UtcNow;

    var now = new DateTimeOffset(
        utcNow.Ticks - (utcNow.Ticks % 10),
        TimeSpan.Zero);

    // Devolve exatamente o valor originalmente cobrado.
    customer.Credit(operation.Amount);

    // Altera o estado da operaÃ§Ã£o.
    operation.MarkRefunded(now);

    // CrÃ©dito financeiro vinculado Ã  operaÃ§Ã£o.
    var refundLedgerEntry = LedgerEntry.CreateRefundCredit(
        Guid.NewGuid(),
        customer.Id,
        operation.Id,
        operation.Amount,
        $"Estorno da operaÃ§Ã£o {operation.Type}",
        now);

    await _dbContext.LedgerEntries.AddAsync(
        refundLedgerEntry,
        cancellationToken);

    // Gera uma Ãºnica identidade para o evento.
    // O mesmo GUID serÃ¡ utilizado na Outbox e no payload.
    var eventId = Guid.NewGuid();

    var eventPayload = JsonSerializer.Serialize(new
    {
      EventId = eventId,
      OperationId = operation.Id,
      ExternalOperationId = operation.ExternalOperationId,
      CustomerId = operation.CustomerId,
      OperationType = operation.Type.ToString(),
      Amount = operation.Amount.Amount,
      RefundedAt = now.UtcDateTime
    });

    var outboxMessage = new OutboxMessage(
        eventId,
        "OperationRefunded.v1",
        operation.Id,
        eventPayload);

    await _dbContext.OutboxMessages.AddAsync(
        outboxMessage,
        cancellationToken);

    await _dbContext.SaveChangesAsync(cancellationToken);

    await transaction.CommitAsync(cancellationToken);

    var detail = await BuildOperationDetailAsync(
        operation,
        cancellationToken);

    return new RefundOperationResult(
        detail,
        false);
  }

  public async Task<OperationDto> CreateAsync(
      CreateOperationCommand command,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (command.ExternalOperationId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador externo da operaÃ§Ã£o Ã© obrigatÃ³rio.");
    }

    if (command.CustomerId == Guid.Empty)
    {
      throw new ArgumentException(
          "O identificador do cliente Ã© obrigatÃ³rio.");
    }

    if (!Enum.TryParse<OperationType>(
            command.OperationType,
            true,
            out var operationType)
        || !Enum.IsDefined(operationType))
    {
      throw new ArgumentException(
          "O tipo de operaÃ§Ã£o informado Ã© invÃ¡lido.");
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

      // Serializa operaÃ§Ãµes concorrentes do mesmo cliente.
      var customer = await _dbContext.Customers
          .FromSqlInterpolated(
              $"SELECT * FROM customers WHERE id = {command.CustomerId} FOR UPDATE")
          .FirstOrDefaultAsync(cancellationToken);

      if (customer is null)
      {
        throw new InvalidOperationException(
            "O cliente informado nÃ£o foi encontrado.");
      }

      // Verifica a idempotÃªncia dentro da transaÃ§Ã£o.
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
        // O dÃ©bito serÃ¡ confirmado junto com a operaÃ§Ã£o,
        // o lanÃ§amento financeiro e a mensagem da Outbox.
        customer.Debit(amount);

        operation.MarkCharged();

        var ledgerEntry = new LedgerEntry(
            Guid.NewGuid(),
            customer.Id,
            operation.Id,
            amount,
            LedgerEntryType.Debit,
            $"CobranÃ§a da operaÃ§Ã£o {operationType}",
            DateTimeOffset.UtcNow);

        await _dbContext.LedgerEntries.AddAsync(
            ledgerEntry,
            cancellationToken);
      }
      else if (customer.Plan == CustomerPlan.Postpaid)
      {
        // A operaÃ§Ã£o ficarÃ¡ disponÃ­vel para o faturamento mensal.
        operation.MarkAwaitingInvoice();
      }
      else
      {
        throw new InvalidOperationException(
            "O plano do cliente Ã© invÃ¡lido.");
      }

      await _dbContext.Operations.AddAsync(
          operation,
          cancellationToken);

      // Gera uma Ãºnica identidade para o evento.
      // O mesmo GUID serÃ¡ utilizado na Outbox e no payload.
      var eventId = Guid.NewGuid();

      var eventPayload = JsonSerializer.Serialize(new
      {
        EventId = eventId,
        OperationId = operation.Id,
        ExternalOperationId = command.ExternalOperationId,
        CustomerId = command.CustomerId,
        OperationType = operationType.ToString(),
        Amount = amount.Amount,
        CustomerPlan = customer.Plan.ToString(),
        CreatedAt = operation.CreatedAt.UtcDateTime
      });

      var outboxMessage = new OutboxMessage(
          eventId,
          "OperationCreated.v1",
          operation.Id,
          eventPayload);

      await _dbContext.OutboxMessages.AddAsync(
          outboxMessage,
          cancellationToken);

      // Persiste operaÃ§Ã£o, lanÃ§amento e Outbox atomicamente.
      await _dbContext.SaveChangesAsync(cancellationToken);

      await transaction.CommitAsync(cancellationToken);

      return Map(operation);
    }
    catch (DbUpdateException exception)
        when (IsOperationUniqueViolation(exception))
    {
      // Descarta o estado local da transaÃ§Ã£o que falhou.
      _dbContext.ChangeTracker.Clear();

      // Uma violaÃ§Ã£o do ID externo pode representar uma
      // requisiÃ§Ã£o concorrente jÃ¡ confirmada.
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

      // Uma violaÃ§Ã£o do hash sem operaÃ§Ã£o encontrada pelo ID
      // externo indica reutilizaÃ§Ã£o de uma chave de requisiÃ§Ã£o.
      if (IsRequestHashUniqueViolation(exception))
      {
        throw new InvalidOperationException(
            "JÃ¡ existe uma operaÃ§Ã£o registrada com os mesmos dados da requisiÃ§Ã£o.",
            exception);
      }

      // NÃ£o transforma outras violaÃ§Ãµes de integridade
      // em respostas idempotentes.
      throw;
    }
  }

  public async Task<OperationDetailDto?> GetByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  {
    var operation = await _dbContext.Operations
        .AsNoTracking()
        .FirstOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

    if (operation is null)
      return null;

    return await BuildOperationDetailAsync(
        operation,
        cancellationToken);
  }

  public async Task<OperationPagedResult> GetAllAsync(
      Guid? customerId,
      string? operationType,
      DateTimeOffset? startDate,
      DateTimeOffset? endDate,
      int page,
      int pageSize,
      CancellationToken cancellationToken = default,
      string? status = null)
  {
    var query = _dbContext.Operations
        .AsNoTracking()
        .AsQueryable();

    if (customerId.HasValue)
    {
      query = query.Where(
          operation => operation.CustomerId == customerId.Value);
    }

    if (!string.IsNullOrWhiteSpace(operationType))
    {
      if (!Enum.TryParse<OperationType>(
              operationType,
              true,
              out var parsedType)
          || !Enum.IsDefined(parsedType))
      {
        throw new ArgumentException(
            "O tipo de operaÃ§Ã£o informado Ã© invÃ¡lido.",
            nameof(operationType));
      }

      query = query.Where(
          operation => operation.Type == parsedType);
    }

    if (status is not null)
    {
      if (!Enum.TryParse<OperationStatus>(
              status,
              true,
              out var parsedStatus)
          || !Enum.IsDefined(parsedStatus))
      {
        throw new ArgumentException(
            "O status da operaÃ§Ã£o informado Ã© invÃ¡lido.",
            nameof(status));
      }

      query = query.Where(
          operation => operation.Status == parsedStatus);
    }

    if (startDate.HasValue)
    {
      query = query.Where(
          operation => operation.CreatedAt >= startDate.Value);
    }

    if (endDate.HasValue)
    {
      query = query.Where(
          operation => operation.CreatedAt <= endDate.Value);
    }

    var totalCount = await query.CountAsync(cancellationToken);

    if (page < 1)
      throw new ArgumentOutOfRangeException(nameof(page));

    if (pageSize < 1 || pageSize > 100)
      throw new ArgumentOutOfRangeException(nameof(pageSize));

    var totalPages = totalCount == 0
        ? 0
        : (int)Math.Ceiling(totalCount / (double)pageSize);

    var operations = await query
        .OrderByDescending(operation => operation.CreatedAt)
        .ThenByDescending(operation => operation.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    var items = operations
        .Select(Map)
        .ToList();

    return new OperationPagedResult(
        items,
        page,
        pageSize,
        totalCount);
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
          "O identificador externo jÃ¡ foi utilizado com dados diferentes.");
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
        continue;

      if (postgresException.SqlState !=
          PostgresErrorCodes.UniqueViolation)
        continue;

      return string.Equals(
                 postgresException.ConstraintName,
                 "ux_operations_external_operation_id",
                 StringComparison.Ordinal)
             || string.Equals(
                 postgresException.ConstraintName,
                 "ux_operations_customer_request_hash",
                 StringComparison.Ordinal);
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
          Id: operation.Id,
          ExternalOperationId: operation.ExternalOperationId,
          CustomerId: operation.CustomerId,
          OperationType: operation.Type.ToString(),
          Amount: operation.Amount.Amount,
          Status: operation.Status.ToString(),
          CreatedAt: operation.CreatedAt,
          RefundedAt: operation.RefundedAt,
          InvoicedAt: operation.InvoicedAt,
          InvoiceId: operation.InvoiceId);

  private async Task<OperationDetailDto> BuildOperationDetailAsync(
      Operation operation,
      CancellationToken cancellationToken)
  {
    var ledgerEntries = await _dbContext.LedgerEntries
        .AsNoTracking()
        .Where(entry => entry.OperationId == operation.Id)
        .OrderBy(entry => entry.CreatedAt)
        .ThenBy(entry => entry.Id)
        .ToListAsync(cancellationToken);

    var ledgerDtos = ledgerEntries
        .Select(entry => new OperationLedgerEntryDto(
            entry.Id,
            entry.Type.ToString(),
            entry.Amount.Amount,
            entry.Description,
            entry.CreatedAt))
        .ToList();

    return new OperationDetailDto(
        operation.Id,
        operation.ExternalOperationId,
        
        operation.CustomerId,
        operation.Type.ToString(),
        operation.Amount.Amount,
        operation.Status.ToString(),
        operation.CreatedAt,
        operation.InvoicedAt,
        operation.RefundedAt,
        operation.InvoiceId,
        ledgerDtos);
  }
}
