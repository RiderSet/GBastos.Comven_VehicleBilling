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
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StatementDto?> GetAsync(
        GetStatementQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customerExists = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == query.CustomerId, cancellationToken);

        if (!customerExists)
            return null;

        var start = new DateTimeOffset(
            query.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var endExclusive = new DateTimeOffset(
            query.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var entries = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.CustomerId == query.CustomerId &&
                e.CreatedAt >= start &&
                e.CreatedAt < endExclusive)
            .Select(e => new { e.Type, Amount = e.Amount.Amount })
            .ToListAsync(cancellationToken);

        var credits = entries
            .Where(e => e.Type == LedgerEntryType.Credit)
            .Sum(e => e.Amount);

        var debits = entries
            .Where(e => e.Type == LedgerEntryType.Debit)
            .Sum(e => e.Amount);

        return new StatementDto(
            query.CustomerId,
            query.StartDate,
            query.EndDate,
            credits,
            debits,
            credits - debits);
    }
}
