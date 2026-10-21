using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
  private readonly VehicleBillingDbContext _context;

  public UnitOfWork(VehicleBillingDbContext context)
  {
    _context = context;
  }

  public Task<int> SaveChangesAsync(
      CancellationToken cancellationToken = default)
  {
    return _context.SaveChangesAsync(cancellationToken);
  }
}
