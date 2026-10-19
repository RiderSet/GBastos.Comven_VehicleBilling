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
    string connectionString = "Host=127.0.0.1;Port=5432;Database=vehicle_billing;Username=billing_app;Password=Gbsft@7K10";

    try
    {
      var secretsPath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
          "Microsoft", "UserSecrets", "d36bc417-ca63-4919-bef9-a07679fd6b7b", "secrets.json");

      if (File.Exists(secretsPath))
      {
        using var document = JsonDocument.Parse(File.ReadAllText(secretsPath));
        if (document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings) &&
            connStrings.TryGetProperty("VehicleBilling", out var val) &&
            !string.IsNullOrEmpty(val.GetString()))
        {
          connectionString = val.GetString()!;
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[Aviso DesignTime] Erro ao ler User Secrets: {ex.Message}");
    }

    // EXIBE A CONNECTION STRING EXATA NO CONSOLE PARA AUDITORIA
    Console.WriteLine($"[DIAGNÓSTICO FINAL] ConnectionString usada: {connectionString}");

    var options = new DbContextOptionsBuilder<VehicleBillingDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    return new VehicleBillingDbContext(options);
  }
}
