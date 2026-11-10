using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Worker.Workers;
using Comven_VehicleBilling.WorkerService;
using Comven_VehicleBilling.WorkerService.Events;

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
        "Arquivo .env nÃ£o encontrado na raiz do repositÃ³rio.");
}

DotNetEnv.Env.Load(envFile);

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

// Registra PostgreSQL, repositÃ³rio da Outbox e RabbitMQ.
builder.Services.AddInfrastructure(builder.Configuration);

// Handlers e consumidores de mensagens.
builder.Services.AddScoped<OperationCreatedHandler>();
builder.Services.AddScoped<OperationRefundedHandler>();

// Publica eventos da Outbox e consome eventos com Inbox/idempotÃªncia.
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<RabbitMqConsumer>();

var host = builder.Build();

await host.RunAsync();

