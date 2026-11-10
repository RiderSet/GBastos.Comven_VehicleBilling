using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Worker.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

static string? FindEnvFile()
{
    var directory = new DirectoryInfo(
        Directory.GetCurrentDirectory());

    while (directory is not null)
    {
        var envPath = Path.Combine(directory.FullName, ".env");

        if (File.Exists(envPath))
            return envPath;

        directory = directory.Parent;
    }

    return null;
}

var envFile = FindEnvFile();

if (envFile is null)
{
    throw new FileNotFoundException(
        "Arquivo .env não encontrado na raiz do repositório.");
}

DotNetEnv.Env.Load(envFile);

var builder = Host.CreateApplicationBuilder(args);

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
