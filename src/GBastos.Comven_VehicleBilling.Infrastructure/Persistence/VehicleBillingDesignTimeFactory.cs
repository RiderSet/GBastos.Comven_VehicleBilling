using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using System.Text.Json;

namespace Comven_VehicleBilling.Infrastructure.Persistence;

public sealed class VehicleBillingDesignTimeFactory
    : IDesignTimeDbContextFactory<VehicleBillingDbContext>
{
  public VehicleBillingDbContext CreateDbContext(string[] args)
  {
    // Localiza a raiz do repositório.
    var repositoryRoot =
        FindRepositoryRoot(Directory.GetCurrentDirectory())
        ?? FindRepositoryRoot(AppContext.BaseDirectory);

    // Carrega o .env sem sobrescrever variáveis existentes.
    if (repositoryRoot is not null)
    {
      LoadEnvFile(Path.Combine(repositoryRoot, ".env"));
    }

    // 1. Conexão exclusiva dos testes.
    var testConnectionString =
        Environment.GetEnvironmentVariable(
            "VEHICLE_BILLING_TEST_CONNECTION_STRING");

    // 2. Conexão padrão do ambiente ou .env.
    var defaultConnectionString =
        Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection");

    string? connectionString = null;

    if (!string.IsNullOrWhiteSpace(testConnectionString))
    {
      connectionString = testConnectionString;
    }
    else if (!string.IsNullOrWhiteSpace(defaultConnectionString))
    {
      connectionString = defaultConnectionString;
    }
    else
    {
      // 3. Alternativa: User Secrets.
      connectionString = LoadUserSecretsConnectionString();
    }

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException(
          "ConnectionStrings__DefaultConnection não configurada. " +
          "Verifique o .env e os User Secrets.");
    }

    var connection =
        new NpgsqlConnectionStringBuilder(connectionString);

    Console.WriteLine(
        $"[DesignTime] PostgreSQL: {connection.Host}:{connection.Port}; " +
        $"Database={connection.Database}; " +
        $"Username={connection.Username}");

    var options =
        new DbContextOptionsBuilder<VehicleBillingDbContext>()
            .UseNpgsql(connection.ConnectionString)
            .Options;

    return new VehicleBillingDbContext(options);
  }

  private static string? FindRepositoryRoot(string startDirectory)
  {
    var directory = new DirectoryInfo(startDirectory);

    while (directory is not null)
    {
      var envPath = Path.Combine(
          directory.FullName, ".env");

      var sourcePath = Path.Combine(
          directory.FullName, "src");

      if (File.Exists(envPath) &&
          Directory.Exists(sourcePath))
      {
        return directory.FullName;
      }

      directory = directory.Parent;
    }

    return null;
  }

  private static void LoadEnvFile(string envPath)
  {
    if (!File.Exists(envPath))
      return;

    foreach (var originalLine in File.ReadLines(envPath))
    {
      var line = originalLine.Trim();

      if (string.IsNullOrWhiteSpace(line) ||
          line.StartsWith("#"))
      {
        continue;
      }

      if (line.StartsWith("export "))
      {
        line = line.Substring(7).Trim();
      }

      var separatorIndex = line.IndexOf('=');

      if (separatorIndex <= 0)
        continue;

      var key = line.Substring(0, separatorIndex).Trim();

      var value = line.Substring(separatorIndex + 1).Trim();

      if (string.IsNullOrWhiteSpace(key))
        continue;

      // Preserva variáveis já definidas no ambiente.
      if (Environment.GetEnvironmentVariable(key) is not null)
        continue;

      // Remove aspas externas, quando existentes.
      if (value.Length >= 2 &&
          ((value.StartsWith("\"") && value.EndsWith("\"")) ||
           (value.StartsWith("'") && value.EndsWith("'"))))
      {
        value = value.Substring(1, value.Length - 2);
      }

      Environment.SetEnvironmentVariable(
          key,
          value,
          EnvironmentVariableTarget.Process);
    }
  }

  private static string? LoadUserSecretsConnectionString()
  {
    var secretsPath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "UserSecrets",
        "d36bc417-ca63-4919-bef9-a07679fd6b7b",
        "secrets.json");

    if (!File.Exists(secretsPath))
      return null;

    using var document =
        JsonDocument.Parse(File.ReadAllText(secretsPath));

    if (document.RootElement.TryGetProperty(
            "ConnectionStrings:DefaultConnection",
            out var value))
    {
      return value.GetString();
    }

    return null;
  }
}
