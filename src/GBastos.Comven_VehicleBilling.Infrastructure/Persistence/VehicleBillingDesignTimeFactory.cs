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

    var connection = new NpgsqlConnectionStringBuilder(
        value.GetString())
    {
      Host = "127.0.0.1",
      Port = 5433
    };

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
