using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Comven_VehicleBilling.Infrastructure.BillingsProvider;
using Comven_VehicleBilling.Infrastructure.Mensageria;
using Comven_VehicleBilling.Infrastructure.Outbox;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

namespace Comven_VehicleBilling.Infrastructure.Persistence;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    AddPersistence(services, configuration);

    services.AddScoped<OutboxRepository>();
    services.AddSingleton<IBillingProvider, BillingProvider>();
    services.AddSingleton<RabbitMqPublisher>();
    services.AddScoped<ICustomerRepository, CustomerRepository>();

    return services;
  }

  private static void AddPersistence(
      IServiceCollection services,
      IConfiguration configuration)
  {
    var connectionString =
        configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException(
          "A connectionstring 'DefaultConnection' não foi configurada.");
    }

    services.AddDbContext<VehicleBillingDbContext>(options =>
        options.UseNpgsql(connectionString));
  }
}
