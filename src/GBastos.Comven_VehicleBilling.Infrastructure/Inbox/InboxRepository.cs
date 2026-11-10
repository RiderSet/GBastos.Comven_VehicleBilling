using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Inbox;

public sealed class InboxRepository
{
  private readonly VehicleBillingDbContext _dbContext;

  public InboxRepository(VehicleBillingDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public Task<bool> ExistsAsync(
      Guid messageId,
      string consumer,
      CancellationToken cancellationToken)
  {
    return _dbContext.InboxMessages.AnyAsync(
        message =>
            message.Id == messageId &&
            message.Consumer == consumer,
        cancellationToken);
  }

  public async Task AddAsync(
      Guid messageId,
      string consumer,
      CancellationToken cancellationToken)
  {
    var message = new InboxMessage(
        messageId,
        consumer);

    await _dbContext.InboxMessages.AddAsync(
        message,
        cancellationToken);
  }
}
