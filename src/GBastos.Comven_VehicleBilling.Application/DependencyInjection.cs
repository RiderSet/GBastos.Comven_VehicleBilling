using Comven_VehicleBilling.Application.Customers;
using Comven_VehicleBilling.Application.Invoices;
using Comven_VehicleBilling.Application.Operations;
using Comven_VehicleBilling.Application.Statements;
using Microsoft.Extensions.DependencyInjection;

namespace Comven_VehicleBilling.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(
      this IServiceCollection services)
  {
    services.AddScoped<CustomerService>();
    services.AddScoped<OperationService>();
    services.AddScoped<StatementService>();
    services.AddScoped<InvoiceService>();

    return services;
  }
}
