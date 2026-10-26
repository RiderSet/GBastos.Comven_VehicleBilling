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
    var lockId = Guid.NewGuid();
    var lockedUntil = now.AddMinutes(5);

    await using var transaction =
        await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

    await _dbContext.Database.ExecuteSqlInterpolatedAsync(
        $"""
            WITH candidates AS
            (
                SELECT "Id"
                FROM outbox_messages
                WHERE "ProcessedAt" IS NULL
                  AND (
                      "NextAttemptAt" IS NULL
                      OR "NextAttemptAt" <= {now}
                  )
                  AND (
                      "LockedUntil" IS NULL
                      OR "LockedUntil" <= {now}
                  )
                ORDER BY "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT {batchSize}
            )
            UPDATE outbox_messages AS message
            SET "LockId" = {lockId},
                "LockedUntil" = {lockedUntil}
            FROM candidates
            WHERE message."Id" = candidates."Id";
            """,
        cancellationToken);

    var messages = await _dbContext.OutboxMessages
        .Where(message =>
            message.LockId == lockId &&
            message.ProcessedAt == null)
        .OrderBy(message => message.CreatedAt)
        .ToListAsync(cancellationToken);

    await transaction.CommitAsync(cancellationToken);

    return messages;
  }

  public async Task SaveChangesAsync(
      CancellationToken cancellationToken = default)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
