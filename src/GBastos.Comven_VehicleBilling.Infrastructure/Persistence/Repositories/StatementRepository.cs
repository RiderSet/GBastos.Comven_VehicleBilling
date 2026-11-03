using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Statements;
using Comven_VehicleBilling.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

public sealed class StatementRepository : IStatementRepository
{
    private readonly VehicleBillingDbContext _dbContext;

    public StatementRepository(VehicleBillingDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StatementDto?> GetAsync(
        GetStatementQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customerExists = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                customer => customer.Id == query.CustomerId,
                cancellationToken);

        if (!customerExists)
            return null;

        var start = new DateTimeOffset(
            query.StartDate.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var endExclusive = new DateTimeOffset(
            query.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var entries = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CustomerId == query.CustomerId &&
                entry.CreatedAt >= start &&
                entry.CreatedAt < endExclusive)
            .Select(entry => new
            {
                entry.Type,
                Amount = entry.Amount.Amount
            })
            .ToListAsync(cancellationToken);

        var credits = entries
            .Where(entry => entry.Type == LedgerEntryType.Credit)
            .Sum(entry => entry.Amount);

        var debits = entries
            .Where(entry => entry.Type == LedgerEntryType.Debit)
            .Sum(entry => entry.Amount);

        return new StatementDto(
            query.CustomerId,
            query.StartDate,
            query.EndDate,
            credits,
            debits,
            credits - debits);
    }
}
