using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Permite obter a connection string por variável de ambiente.
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<OutboxWorker>();

await builder.Build().RunAsync();
