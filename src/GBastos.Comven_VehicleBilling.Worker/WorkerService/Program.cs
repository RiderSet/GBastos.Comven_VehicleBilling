using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Worker.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();

host.Run();
