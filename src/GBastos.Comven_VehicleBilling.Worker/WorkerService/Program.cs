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
        "Arquivo .env nÃƒÂ£o encontrado na raiz do repositÃƒÂ³rio.");
}

DotNetEnv.Env.Load(envFile);

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

// Registra PostgreSQL, repositÃƒÂ³rio da Outbox e RabbitMQ.
builder.Services.AddInfrastructure(builder.Configuration);

// Utiliza o mesmo relógio simulado da API durante os testes funcionais.
if (builder.Environment.IsDevelopment())
{
  var simulatedNow = builder.Configuration[
      "VEHICLE_BILLING_TEST_NOW_UTC"];

  if (!string.IsNullOrWhiteSpace(simulatedNow))
  {
    builder.Services.AddSingleton<TimeProvider>(
        new DevelopmentTimeProvider(simulatedNow));
  }
}

// Handlers e consumidores de mensagens.
builder.Services.AddScoped<IOperationCreatedHandler, OperationCreatedHandler>();
builder.Services.AddScoped<OperationRefundedHandler>();
builder.Services.AddScoped<InvoiceCreatedHandler>();
builder.Services.AddScoped<SupplementaryInvoiceCreatedHandler>();

// Configurações centralizadas do consumidor RabbitMQ.
builder.Services.AddSingleton<RabbitMqConsumerOptions>(serviceProvider =>
    RabbitMqConsumerOptions.FromConfiguration(
        serviceProvider.GetRequiredService<IConfiguration>()));
// Publica eventos da Outbox e consome eventos com Inbox/idempotÃƒÂªncia.
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<RabbitMqConsumer>();

var host = builder.Build();

await host.RunAsync();



