using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Worker.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Carrega configurações locais, quando existirem.
// Variáveis de ambiente têm precedência.
builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

// Registra PostgreSQL, repositório da Outbox e RabbitMQ.
builder.Services.AddInfrastructure(builder.Configuration);

// Inicia o processamento da Outbox em segundo plano.
builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();

await host.RunAsync();