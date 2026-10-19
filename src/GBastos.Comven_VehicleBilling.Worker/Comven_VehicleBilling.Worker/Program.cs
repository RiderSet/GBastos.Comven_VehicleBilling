using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Worker.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Carrega configurações locais, se existirem.
// Variáveis de ambiente também podem fornecer a connection string.
builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

// Registra banco de dados e serviços da Infrastructure.
builder.Services.AddInfrastructure(builder.Configuration);

// Executa o processamento da Outbox em segundo plano.
builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();

await host.RunAsync();
