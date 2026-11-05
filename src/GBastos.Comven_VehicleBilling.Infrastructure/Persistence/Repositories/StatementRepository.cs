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

  public async Task<StatementPageDto> GetEntriesAsync(
      GetStatementEntriesQuery query,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (query.CustomerId == Guid.Empty)
      throw new ArgumentException(
          "O ID do cliente é obrigatório.",
          nameof(query));

    if (query.Page < 1)
      throw new ArgumentOutOfRangeException(
          nameof(query.Page),
          "A página deve ser maior que zero.");

    if (query.PageSize < 1 || query.PageSize > 100)
      throw new ArgumentOutOfRangeException(
          nameof(query.PageSize),
          "O tamanho da página deve estar entre 1 e 100.");

    var customerExists = await _dbContext.Customers
        .AsNoTracking()
        .AnyAsync(
            customer => customer.Id == query.CustomerId,
            cancellationToken);

    if (!customerExists)
      throw new KeyNotFoundException(
          "Cliente não encontrado.");

    var entriesQuery = _dbContext.LedgerEntries
        .AsNoTracking()
        .Where(entry => entry.CustomerId == query.CustomerId);

    var totalItems = await entriesQuery
        .CountAsync(cancellationToken);

    var skip = checked((query.Page - 1) * query.PageSize);

    var items = await entriesQuery
        .OrderByDescending(entry => entry.CreatedAt)
        .ThenByDescending(entry => entry.Id)
        .Skip(skip)
        .Take(query.PageSize)
        .Select(entry => new StatementEntryDto(
            entry.Id,
            entry.OperationId,
            entry.BalanceCreditId,
            entry.Amount.Amount,
            entry.Type == LedgerEntryType.Credit
                ? "Credit"
                : "Debit",
            entry.Description,
            entry.CreatedAt))
        .ToListAsync(cancellationToken);

    return new StatementPageDto(
        query.CustomerId,
        query.Page,
        query.PageSize,
        totalItems,
        items);
  }
}
