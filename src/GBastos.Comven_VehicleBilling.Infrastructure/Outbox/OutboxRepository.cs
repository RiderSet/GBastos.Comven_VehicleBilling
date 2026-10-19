using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Outbox;

public sealed class OutboxRepository
{
  private readonly VehicleBillingDbContext _dbContext;

  public OutboxRepository(VehicleBillingDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
      int batchSize,
      CancellationToken cancellationToken = default)
  {
    if (batchSize <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(batchSize));
    }

    var now = DateTime.UtcNow;

    return await _dbContext.OutboxMessages
        .Where(message =>
            message.ProcessedAt == null &&
            (message.NextAttemptAt == null ||
             message.NextAttemptAt <= now))
        .OrderBy(message => message.CreatedAt)
        .Take(batchSize)
        .ToListAsync(cancellationToken);
  }

  public async Task SaveChangesAsync(
      CancellationToken cancellationToken = default)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
