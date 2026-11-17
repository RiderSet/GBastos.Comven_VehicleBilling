using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Comven_VehicleBilling.IntegrationTests;

internal static class IntegrationTestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        string? envFile = null;

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");

            if (File.Exists(candidate))
            {
                envFile = candidate;
                break;
            }

            directory = directory.Parent;
        }

        if (envFile is null)
        {
            throw new FileNotFoundException(
                "Arquivo .env não encontrado na raiz do repositório.");
        }

        DotNetEnv.Env.Load(envFile);

        var defaultConnection =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection não foi encontrada no .env.");
        }

        var connectionBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = defaultConnection
        };

        if (!connectionBuilder.ContainsKey("Database"))
        {
            throw new InvalidOperationException(
                "A connection string principal não contém Database.");
        }

        var originalDatabase =
            Convert.ToString(connectionBuilder["Database"]);

        if (!string.Equals(
                originalDatabase,
                "vehicle_billing",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O banco principal precisa ser vehicle_billing. " +
                "A conexão de testes não foi configurada.");
        }

        connectionBuilder["Database"] = "vehicle_billing_test";

        Environment.SetEnvironmentVariable(
            "VEHICLE_BILLING_TEST_CONNECTION_STRING",
            connectionBuilder.ConnectionString);
    }
}
