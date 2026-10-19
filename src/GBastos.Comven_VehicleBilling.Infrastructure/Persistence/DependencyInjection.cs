using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comven_VehicleBilling.Infrastructure.Persistence;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    AddPersistence(services, configuration);

    // Registrar aqui os serviços do Outbox.
    // Registrar aqui o cliente do BillingProvider.

    return services;
  }

  private static void AddPersistence(
      IServiceCollection services,
      IConfiguration configuration)
  {
    var connectionString =
        configuration.GetConnectionString("VehicleBilling");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException(
          "A connection string 'VehicleBilling' não foi configurada.");
    }

    services.AddDbContext<VehicleBillingDbContext>(options =>
        options.UseNpgsql(connectionString));

  }
}
