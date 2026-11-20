using System.Collections.Concurrent;
using System.Diagnostics;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.WorkerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Comven_VehicleBilling.IntegrationTests.Messaging;

[Collection("PostgreSQL integration tests")]
public sealed class RabbitMqResilienceIntegrationTests
{
    [Fact]
    public async Task ConsumerMustReconnectAfterRabbitMqBecomesUnavailableAndRestarts()
    {
        // Execução explicitamente habilitada para evitar interromper
        // o RabbitMQ durante a execução normal da suíte.
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    "RUN_RABBITMQ_RESILIENCE_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hostName = RequiredEnvironmentVariable("RABBITMQ_HOST");
        var userName = RequiredEnvironmentVariable("RABBITMQ_USER");
        var password = RequiredEnvironmentVariable("RABBITMQ_PASSWORD");
        var virtualHost = RequiredEnvironmentVariable("RABBITMQ_VHOST");

        var testConnectionString =
            RequiredEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        var portText = Environment.GetEnvironmentVariable("RABBITMQ_PORT");

        var port = int.TryParse(portText, out var configuredPort)
            ? configuredPort
            : 5672;

        var containerName =
            Environment.GetEnvironmentVariable("RABBITMQ_TEST_CONTAINER")
            ?? "comven-rabbit";

        var suffix = Guid.NewGuid().ToString("N");

        var queueName = $"vehicle-billing.resilience.{suffix}";

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(120));

        var logProvider = new TestLoggerProvider();

        ServiceProvider? serviceProvider = null;
        var brokerStopped = false;

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            testConnectionString
                    })
                .Build();

            var options = new RabbitMqConsumerOptions
            {
                QueueName = queueName,
                ConsumerName = $"resilience-consumer-{suffix}",
                DeadLetterExchange = $"{queueName}.dlx",
                DeadLetterQueue = $"{queueName}.dlq",
                HostName = hostName,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                Port = port,
                ReconnectIntervalSeconds = 1,
                MaxRetryAttempts = 3,
                RetryHeader = "x-retry-count",
                RetryQueues =
                [
                    new RetryQueueOptions(
                        $"{queueName}.retry.1s", 1_000),
                    new RetryQueueOptions(
                        $"{queueName}.retry.2s", 2_000),
                    new RetryQueueOptions(
                        $"{queueName}.retry.3s", 3_000)
                ]
            };

            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddProvider(logProvider);
            });

            services.AddInfrastructure(configuration);
            services.AddSingleton(options);
            services.AddSingleton<RabbitMqConsumer>();

            serviceProvider = services.BuildServiceProvider();

            var consumer =
                serviceProvider.GetRequiredService<RabbitMqConsumer>();

            // CENÁRIO 1:
            // O RabbitMQ é interrompido antes da inicialização
            // do consumidor.
            await RunDockerAsync(
                "stop",
                containerName,
                timeout.Token);

            brokerStopped = true;

            await consumer.StartAsync(timeout.Token);

            await logProvider.WaitForAsync(
                log => log.Contains(
                    "Falha no Consumer RabbitMQ",
                    StringComparison.Ordinal),
                timeout.Token);

            // O consumidor já tentou conectar e falhou.
            // Agora o RabbitMQ retorna.
            await RunDockerAsync(
                "start",
                containerName,
                timeout.Token);

            brokerStopped = false;

            await logProvider.WaitForCountAsync(
                log => log.Contains(
                    "Fila principal:",
                    StringComparison.Ordinal),
                expectedCount: 1,
                timeout.Token);

            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"aguardando mensagens na fila {queueName}",
                    StringComparison.Ordinal),
                timeout.Token);

            // CENÁRIO 2:
            // Interrompe o RabbitMQ com o consumidor conectado.
            await RunDockerAsync(
                "stop",
                containerName,
                timeout.Token);

            brokerStopped = true;

            await logProvider.WaitForAsync(
                log =>
                    log.Contains(
                        "Nova conex",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    log.Contains(
                        "Falha no Consumer RabbitMQ",
                        StringComparison.Ordinal),
                timeout.Token);

            // Reinicia o broker.
            await RunDockerAsync(
                "start",
                containerName,
                timeout.Token);

            brokerStopped = false;

            // Deve existir uma segunda conexão estabelecida.
            await logProvider.WaitForCountAsync(
                log => log.Contains(
                    "Fila principal:",
                    StringComparison.Ordinal),
                expectedCount: 2,
                timeout.Token);

            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"aguardando mensagens na fila {queueName}",
                    StringComparison.Ordinal),
                timeout.Token);

            Assert.True(
                logProvider.CountMatching(
                    log => log.Contains(
                        "Fila principal:",
                        StringComparison.Ordinal)) >= 2,
                "O consumidor não restabeleceu a conexão após o reinício do RabbitMQ.");
        }
        finally
        {
            if (brokerStopped)
            {
                using var recoveryTimeout =
                    new CancellationTokenSource(TimeSpan.FromSeconds(60));

                await RunDockerAsync(
                    "start",
                    containerName,
                    recoveryTimeout.Token);
            }

            if (serviceProvider is not null)
            {
                var consumer =
                    serviceProvider.GetRequiredService<RabbitMqConsumer>();

                await consumer.StopAsync(
                    CancellationToken.None);

                await serviceProvider.DisposeAsync();
            }
        }
    }

    private static async Task RunDockerAsync(
        string command,
        string containerName,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(containerName);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Não foi possível iniciar o Docker CLI.");

        var outputTask =
            process.StandardOutput.ReadToEndAsync(cancellationToken);

        var errorTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker {command} {containerName} falhou. " +
                $"Saída: {output} Erro: {error}");
        }
    }

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"A variável de ambiente {name} não foi configurada.");
        }

        return value;
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public ILogger CreateLogger(string categoryName) =>
            new TestLogger(_messages);

        public void Dispose()
        {
        }

        public int CountMatching(Func<string, bool> condition) =>
            _messages.Count(condition);

        public async Task WaitForAsync(
            Func<string, bool> condition,
            CancellationToken cancellationToken)
        {
            while (!_messages.Any(condition))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(100),
                    cancellationToken);
            }
        }

        public async Task WaitForCountAsync(
            Func<string, bool> condition,
            int expectedCount,
            CancellationToken cancellationToken)
        {
            while (CountMatching(condition) < expectedCount)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(100),
                    cancellationToken);
            }
        }

        private sealed class TestLogger(
            ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) =>
                logLevel >= LogLevel.Information;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                var message = formatter(state, exception);

                messages.Enqueue(message);

                Console.WriteLine(
                    $"[RABBITMQ RESILIENCE] {message}");

                if (exception is not null)
                    Console.WriteLine(exception);
            }
        }
    }
}
