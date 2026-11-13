using System.Text.Json;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Invoices;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.Rules;
using Comven_VehicleBilling.Domain.ValueObjects;
using Comven_VehicleBilling.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly VehicleBillingDbContext _dbContext;

    public InvoiceRepository(VehicleBillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

public async Task<InvoiceDto> CreateAsync(
    CreateInvoiceCommand command,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(command);

    if (command.CustomerId == Guid.Empty)
        throw new ArgumentException(
            "CustomerId cannot be empty.",
            nameof(command.CustomerId));

    var billingPeriod = BillingPeriod.ForMonth(
        command.ReferenceDate.Year,
        command.ReferenceDate.Month);

    var periodStart = billingPeriod.StartDate;
    var periodEnd = billingPeriod.EndDateExclusive;

    await using var transaction =
        await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

    try
    {
        // 1. Bloqueia o cliente para serializar
        // a geraÃ§Ã£o de faturas do mesmo cliente.
        var customer = await _dbContext.Customers
            .FromSqlInterpolated(
                $"SELECT * FROM customers WHERE id = {command.CustomerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (customer is null)
            throw new InvalidOperationException(
                "Customer not found.");

        if (customer.Plan != CustomerPlan.Postpaid)
            throw new InvalidOperationException(
                "Invoices can only be generated for postpaid customers.");

        // 2. Captura um Ãºnico instante para toda a operaÃ§Ã£o.
        var now = DateTimeOffset.UtcNow;

        // 3. Verifica se a fatura jÃ¡ foi gerada.
        // Uma nova tentativa deve retornar a fatura existente.
        var existingInvoice = await _dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CustomerId == command.CustomerId
                && invoice.PeriodStart == periodStart
                && invoice.PeriodEnd == periodEnd
                && invoice.OriginalInvoiceId == null)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingInvoice is not null)
            return Map(existingInvoice);

        // 4. Valida o perÃ­odo antes de buscar operaÃ§Ãµes.
        if (!BillingPeriodRules.CanGenerateInvoice(
                billingPeriod,
                now))
        {
            throw new InvalidOperationException(
                "The billing period cannot generate an invoice yet.");
        }

        // 5. Busca as operaÃ§Ãµes pendentes do perÃ­odo.
        var periodStartUtc = new DateTimeOffset(
            periodStart.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var periodEndUtc = new DateTimeOffset(
            periodEnd.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var operations = await _dbContext.Operations
            .Where(operation =>
                operation.CustomerId == command.CustomerId
                && operation.Status == OperationStatus.AwaitingInvoice
                && operation.CreatedAt >= periodStartUtc
                && operation.CreatedAt < periodEndUtc)
            .OrderBy(operation => operation.CreatedAt)
            .ThenBy(operation => operation.Id)
            .ToListAsync(cancellationToken);

        if (operations.Count == 0)
        {
            throw new InvalidOperationException(
                "There are no pending operations for the billing period.");
        }

        // 6. Cria a fatura.
        var invoice = new Invoice(
            Guid.NewGuid(),
            command.CustomerId,
            periodStart,
            periodEnd,
            now);

        foreach (var operation in operations)
        {
            var item = new InvoiceItem(
                invoice.Id,
                operation.Id,
                operation.Amount);

            invoice.AddItem(item);

            operation.MarkInvoiced(
                invoice.Id,
                now);
        }

        invoice.FinalizeInvoice();

        _dbContext.Invoices.Add(invoice);

        // 7. Registra o evento na Outbox.
        var eventId = Guid.NewGuid();

        var payload = JsonSerializer.Serialize(new
        {
            EventId = eventId,
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd,
            Total = invoice.Total.Amount,
            CreatedAt = invoice.CreatedAt,
            Items = invoice.Items.Select(item => new
            {
                OperationId = item.OperationId,
                Amount = item.Amount.Amount
            })
        });

        _dbContext.OutboxMessages.Add(
            new OutboxMessage(
                eventId,
                "InvoiceCreated.v1",
                invoice.Id,
                payload));

        // 8. Persiste fatura, itens, operaÃ§Ãµes e Outbox
        // na mesma transaÃ§Ã£o.
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(invoice);
    }
    catch
    {
        if (_dbContext.Database.CurrentTransaction is not null)
            await transaction.RollbackAsync(CancellationToken.None);

        throw;
    }
}

public async Task<InvoiceDto> CreateSupplementaryAsync(
    CreateSupplementaryInvoiceCommand command,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(command);

    if (command.CustomerId == Guid.Empty)
        throw new ArgumentException(
            "CustomerId cannot be empty.",
            nameof(command.CustomerId));

    if (command.OriginalInvoiceId == Guid.Empty)
        throw new ArgumentException(
            "OriginalInvoiceId cannot be empty.",
            nameof(command.OriginalInvoiceId));

    ArgumentException.ThrowIfNullOrWhiteSpace(
        command.IdempotencyKey);

    var idempotencyKey = command.IdempotencyKey.Trim();

    if (idempotencyKey.Length > 200)
        throw new ArgumentException(
            "IdempotencyKey cannot exceed 200 characters.",
            nameof(command.IdempotencyKey));

    await using var transaction =
        await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

    try
    {
        // Serializa a geração de faturas do mesmo cliente.
        var customer = await _dbContext.Customers
            .FromSqlInterpolated(
                $"SELECT * FROM customers WHERE id = {command.CustomerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (customer is null)
            throw new InvalidOperationException(
                "Customer not found.");

        if (customer.Plan != CustomerPlan.Postpaid)
            throw new InvalidOperationException(
                "Supplementary invoices can only be generated for postpaid customers.");

        var originalInvoice = await _dbContext.Invoices
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invoice => invoice.Id == command.OriginalInvoiceId,
                cancellationToken);

        if (originalInvoice is null)
            throw new InvalidOperationException(
                "Original invoice not found.");

        if (originalInvoice.CustomerId != command.CustomerId)
            throw new InvalidOperationException(
                "The original invoice does not belong to the informed customer.");

        if (originalInvoice.OriginalInvoiceId.HasValue)
            throw new InvalidOperationException(
                "A supplementary invoice cannot be used as the original invoice.");

        // A idempotência é verificada antes das demais regras de criação.
        var existingInvoice = await _dbContext.Invoices
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invoice =>
                    invoice.OriginalInvoiceId == command.OriginalInvoiceId
                    && invoice.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existingInvoice is not null)
            return Map(existingInvoice);

        // Somente faturas geradas e disponíveis para cobrança
        // podem originar uma fatura complementar.
        if (originalInvoice.Status != InvoiceStatus.PaymentPending
            && originalInvoice.Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException(
                "The original invoice must be awaiting payment or already paid.");
        }

        var now = DateTimeOffset.UtcNow;

        var periodStartUtc = new DateTimeOffset(
            originalInvoice.PeriodStart.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var periodEndUtc = new DateTimeOffset(
            originalInvoice.PeriodEnd.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        // Somente operações ainda não faturadas dentro do período
        // da fatura original podem entrar na complementar.
        var operations = await _dbContext.Operations
            .Where(operation =>
                operation.CustomerId == command.CustomerId
                && operation.Status == OperationStatus.AwaitingInvoice
                && operation.CreatedAt >= periodStartUtc
                && operation.CreatedAt < periodEndUtc)
            .OrderBy(operation => operation.CreatedAt)
            .ThenBy(operation => operation.Id)
            .ToListAsync(cancellationToken);

        if (operations.Count == 0)
        {
            throw new InvalidOperationException(
                "There are no pending operations for the original invoice period.");
        }

        var invoice = new Invoice(
            Guid.NewGuid(),
            command.CustomerId,
            originalInvoice.PeriodStart,
            originalInvoice.PeriodEnd,
            now,
            originalInvoice.Id,
            idempotencyKey);

        foreach (var operation in operations)
        {
            var item = new InvoiceItem(
                invoice.Id,
                operation.Id,
                operation.Amount);

            invoice.AddItem(item);

            operation.MarkInvoiced(
                invoice.Id,
                now);
        }

        invoice.FinalizeInvoice();

        _dbContext.Invoices.Add(invoice);

        // Evento específico para permitir que os consumidores
        // distingam faturamento original de faturamento complementar.
        var eventId = Guid.NewGuid();

        var payload = JsonSerializer.Serialize(new
        {
            EventId = eventId,
            InvoiceId = invoice.Id,
            OriginalInvoiceId = originalInvoice.Id,
            IdempotencyKey = idempotencyKey,
            CustomerId = invoice.CustomerId,
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd,
            Total = invoice.Total.Amount,
            CreatedAt = invoice.CreatedAt,
            Items = invoice.Items.Select(item => new
            {
                OperationId = item.OperationId,
                Amount = item.Amount.Amount
            })
        });

        _dbContext.OutboxMessages.Add(
            new OutboxMessage(
                eventId,
                "SupplementaryInvoiceCreated.v1",
                invoice.Id,
                payload));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(invoice);
    }
    catch
    {
        if (_dbContext.Database.CurrentTransaction is not null)
            await transaction.RollbackAsync(CancellationToken.None);

        throw;
    }
}

public async Task<InvoiceDto?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken = default)
{
    if (id == Guid.Empty)
        throw new ArgumentException(
            "Invoice id cannot be empty.",
            nameof(id));

    var invoice = await _dbContext.Invoices
        .AsNoTracking()
        .SingleOrDefaultAsync(
            invoice => invoice.Id == id,
            cancellationToken);

    return invoice is null
        ? null
        : Map(invoice);
}

public async Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
    Guid? customerId = null,
    CancellationToken cancellationToken = default)
{
    var query = _dbContext.Invoices
        .AsNoTracking()
        .AsQueryable();

    if (customerId.HasValue)
    {
        query = query.Where(
            invoice => invoice.CustomerId == customerId.Value);
    }

    var invoices = await query
        .OrderByDescending(invoice => invoice.CreatedAt)
        .ToListAsync(cancellationToken);

    return invoices
        .Select(Map)
        .ToList();
}

private static InvoiceDto Map(Invoice invoice)
    => new(
        invoice.Id,
        invoice.CustomerId,
        invoice.PeriodStart,
        invoice.PeriodEnd,
        invoice.Total.Amount,
        invoice.Status.ToString(),
        invoice.CreatedAt,
        invoice.OriginalInvoiceId);

}
