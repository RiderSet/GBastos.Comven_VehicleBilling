using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.Worker.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Carrega o .env localizado na raiz da solução.
var envFile = Path.Combine(
    Directory.GetCurrentDirectory(),
    ".env");

if (!File.Exists(envFile))
    throw new FileNotFoundException("Arquivo .env não encontrado.", envFile);

foreach (var line in File.ReadAllLines(envFile))
{
    var entry = line.Trim();

    if (string.IsNullOrWhiteSpace(entry) || entry.StartsWith("#"))
        continue;

    var separator = entry.IndexOf('=');

    if (separator <= 0)
        continue;

    var key = entry[..separator].Trim();
    var value = entry[(separator + 1)..].Trim();

    if (value.Length >= 2 &&
        ((value.StartsWith('"') && value.EndsWith('"')) ||
         (value.StartsWith("'") && value.EndsWith("'"))))
    {
        value = value[1..^1];
    }

    Environment.SetEnvironmentVariable(key, value);
}

// Carrega configurações e variáveis de ambiente.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Confirma a configuração antes de registrar a Infrastructure.
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "DefaultConnection não foi carregada pelo Worker.");

// Registra infraestrutura e processamento da Outbox.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();

await host.RunAsync();
