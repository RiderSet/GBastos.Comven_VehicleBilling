using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Invoices;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly VehicleBillingDbContext _dbContext;

    public InvoiceRepository(VehicleBillingDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<InvoiceDto> CreateAsync(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var periodStart = new DateOnly(
            command.ReferenceDate.Year,
            command.ReferenceDate.Month,
            1);

        var periodEnd = periodStart.AddMonths(1);

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(
                c => c.Id == command.CustomerId,
                cancellationToken);

        if (customer is null)
            throw new InvalidOperationException(
                "O cliente informado não foi encontrado.");

        if (customer.Plan != CustomerPlan.Postpaid)
            throw new InvalidOperationException(
                "Apenas clientes pós-pagos podem receber faturas.");

        var existing = await _dbContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.CustomerId == command.CustomerId
                    && i.PeriodStart == periodStart
                    && i.PeriodEnd == periodEnd,
                cancellationToken);

        if (existing is not null)
            return Map(existing);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var operations = await _dbContext.Operations
                .Where(o =>
                    o.CustomerId == command.CustomerId &&
                    o.Status == OperationStatus.AwaitingInvoice &&
                    o.CreatedAt >= new DateTimeOffset(
                        periodStart.ToDateTime(TimeOnly.MinValue),
                        TimeSpan.Zero) &&
                    o.CreatedAt < new DateTimeOffset(
                        periodEnd.ToDateTime(TimeOnly.MinValue),
                        TimeSpan.Zero))
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(cancellationToken);

            var invoice = new Invoice(
                Guid.NewGuid(),
                command.CustomerId,
                periodStart,
                periodEnd,
                DateTimeOffset.UtcNow);

            foreach (var operation in operations)
            {
                var item = new InvoiceItem(
                    invoice.Id,
                    operation.Id,
                    operation.Amount);

                invoice.AddItem(item);

                operation.MarkInvoiced(
                    invoice.Id,
                    DateTimeOffset.UtcNow);
            }

            if (operations.Count > 0)
                invoice.FinalizeInvoice();

            await _dbContext.Invoices.AddAsync(
                invoice,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Map(invoice);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<InvoiceDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.Id == id,
                cancellationToken);

        return invoice is null ? null : Map(invoice);
    }

    public async Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Invoices
            .AsNoTracking()
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(
                i => i.CustomerId == customerId.Value);

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invoices.Select(Map).ToList();
    }

    private static InvoiceDto Map(Invoice invoice)
        => new(
            invoice.Id,
            invoice.CustomerId,
            invoice.PeriodStart,
            invoice.Total.Amount,
            invoice.Status.ToString(),
            invoice.CreatedAt.UtcDateTime);
}
