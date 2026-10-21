namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
  Task<int> SaveChangesAsync(
      CancellationToken cancellationToken = default);
}
