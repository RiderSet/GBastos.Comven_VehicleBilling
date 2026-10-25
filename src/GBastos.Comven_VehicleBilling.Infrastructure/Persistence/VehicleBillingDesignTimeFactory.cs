using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Comven_VehicleBilling.Infrastructure.Persistence;

public sealed class VehicleBillingDesignTimeFactory
    : IDesignTimeDbContextFactory<VehicleBillingDbContext>
{
    public VehicleBillingDbContext CreateDbContext(string[] args)
    {
        // A variável de teste tem prioridade sobre os User Secrets.
        var testConnectionString =
            Environment.GetEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        string connectionString;

        if (!string.IsNullOrWhiteSpace(testConnectionString))
        {
            connectionString = testConnectionString;
        }
        else
        {
            var secretsPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "UserSecrets",
                "d36bc417-ca63-4919-bef9-a07679fd6b7b",
                "secrets.json");

            if (!File.Exists(secretsPath))
            {
                throw new InvalidOperationException(
                    $"Arquivo de User Secrets não encontrado: {secretsPath}");
            }

            using var document =
                JsonDocument.Parse(File.ReadAllText(secretsPath));

            if (!document.RootElement.TryGetProperty(
                    "ConnectionStrings:DefaultConnection",
                    out var value) ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection não foi configurada.");
            }

            connectionString = value.GetString()!;
        }

        var connection =
            new NpgsqlConnectionStringBuilder(connectionString);

        // Não sobrescrevemos host e porta: respeitamos a conexão selecionada.
        Console.WriteLine(
            $"[DesignTime] PostgreSQL: {connection.Host}:{connection.Port}; " +
            $"Database={connection.Database}; Username={connection.Username}");

        var options =
            new DbContextOptionsBuilder<VehicleBillingDbContext>()
                .UseNpgsql(connection.ConnectionString)
                .Options;

        return new VehicleBillingDbContext(options);
    }
}