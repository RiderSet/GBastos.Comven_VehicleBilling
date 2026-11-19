using System.Collections.Concurrent;
using System.Text.Json;
using Comven_VehicleBilling.Infrastructure.Inbox;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.WorkerService;
using Comven_VehicleBilling.WorkerService.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Comven_VehicleBilling.IntegrationTests.Messaging;

[Collection("PostgreSQL integration tests")]
public sealed class OperationCreatedEndToEndTests
{
    [Fact]
    public async Task ShouldProcessOperationCreatedOnlyOnceWhenSameMessageIsPublishedTwice()
    {
        var testConnectionString =
            Environment.GetEnvironmentVariable(
                "VEHICLE_BILLING_TEST_CONNECTION_STRING");

        Assert.False(
            string.IsNullOrWhiteSpace(testConnectionString),
            "VEHICLE_BILLING_TEST_CONNECTION_STRING não foi configurada.");

        var hostName = RequiredEnvironmentVariable("RABBITMQ_HOST");
        var userName = RequiredEnvironmentVariable("RABBITMQ_USER");
        var password = RequiredEnvironmentVariable("RABBITMQ_PASSWORD");
        var virtualHost = RequiredEnvironmentVariable("RABBITMQ_VHOST");

        var portText = Environment.GetEnvironmentVariable("RABBITMQ_PORT");

        var port = int.TryParse(portText, out var configuredPort)
            ? configuredPort
            : 5672;

        var suffix = Guid.NewGuid().ToString("N");

        var queueName = $"vehicle-billing.integration.{suffix}";
        var consumerName = $"integration-consumer-{suffix}";
        var deadLetterExchange = $"vehicle-billing.integration.dlx.{suffix}";
        var deadLetterQueue = $"vehicle-billing.integration.dlq.{suffix}";

        var messageId = Guid.NewGuid();

        var operationEvent = new OperationCreatedEvent(
            EventId: messageId,
            OperationId: Guid.NewGuid(),
            ExternalOperationId: $"integration-{suffix}",
            CustomerId: Guid.NewGuid(),
            OperationType: "VehicleConsultation",
            Amount: 12.50m,
            CustomerPlan: "Prepaid",
            CreatedAt: DateTime.UtcNow);

        var body = JsonSerializer.SerializeToUtf8Bytes(operationEvent);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(40));

        var logProvider = new TestLoggerProvider();

        ServiceProvider? serviceProvider = null;
        IConnection? publisherConnection = null;
        IChannel? publisherChannel = null;

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
                ConsumerName = consumerName,
                DeadLetterExchange = deadLetterExchange,
                DeadLetterQueue = deadLetterQueue,
                HostName = hostName,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                Port = port,
                ReconnectIntervalSeconds = 1,
                MaxRetryAttempts = 1,
                RetryQueues = Array.Empty<RetryQueueOptions>()
            };

            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddProvider(logProvider);
            });

            services.AddInfrastructure(configuration);

            services.AddScoped<OperationCreatedHandler>();

            services.AddSingleton(options);
            services.AddSingleton<RabbitMqConsumer>();

            serviceProvider = services.BuildServiceProvider();

            var consumer =
                serviceProvider.GetRequiredService<RabbitMqConsumer>();

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                AutomaticRecoveryEnabled = false
            };

            publisherConnection =
                await factory.CreateConnectionAsync(timeout.Token);

            publisherChannel =
                await publisherConnection.CreateChannelAsync(
                    cancellationToken: timeout.Token);

            await consumer.StartAsync(timeout.Token);

            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"fila {queueName}",
                    StringComparison.OrdinalIgnoreCase),
                timeout.Token);

            var properties = new BasicProperties
            {
                MessageId = messageId.ToString(),
                Type = "OperationCreated.v1",
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            // Primeira publicação.
            await publisherChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: timeout.Token);

            // Aguarda o primeiro processamento e seu ACK.
            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"Mensagem {messageId} confirmada no RabbitMQ",
                    StringComparison.Ordinal),
                timeout.Token);

            // Segunda publicação: mesmo MessageId e mesmo EventId.
            await publisherChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: timeout.Token);

            // Confirma que a segunda entrega foi reconhecida como duplicada.
            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"Mensagem {messageId} já consta na Inbox",
                    StringComparison.Ordinal),
                timeout.Token);

            // Aguarda também o ACK da segunda entrega.
            await logProvider.WaitForCountAsync(
                logs => logs.Count(entry => entry.Contains(
                    $"Mensagem {messageId} confirmada no RabbitMQ",
                    StringComparison.Ordinal)) >= 2,
                timeout.Token);

            var handlerExecutions = logProvider.Messages.Count(
                log => log.StartsWith(
                    $"Evento {messageId} aceito.",
                    StringComparison.Ordinal));

            Assert.Equal(1, handlerExecutions);

            await using var verificationDb =
                new VehicleBillingDbContext(
                    new DbContextOptionsBuilder<VehicleBillingDbContext>()
                        .UseNpgsql(testConnectionString)
                        .Options);

            var inboxRecords = await verificationDb
                .Set<InboxMessage>()
                .CountAsync(
                    item => item.Id == messageId &&
                            item.Consumer == consumerName,
                    timeout.Token);

            Assert.Equal(1, inboxRecords);

            await consumer.StopAsync(
                CancellationToken.None);
        }
        finally
        {
            if (serviceProvider is not null)
            {
                var consumer =
                    serviceProvider.GetRequiredService<RabbitMqConsumer>();

                try
                {
                    await consumer.StopAsync(
                        new CancellationTokenSource(
                            TimeSpan.FromSeconds(10)).Token);
                }
                catch
                {
                    // A limpeza dos recursos exclusivos continua.
                }
            }

            if (publisherChannel is not null)
            {
                try
                {
                    if (publisherChannel.IsOpen)
                    {
                        await publisherChannel.QueueDeleteAsync(
                            queue: queueName);

                        await publisherChannel.QueueDeleteAsync(
                            queue: deadLetterQueue);

                        await publisherChannel.ExchangeDeleteAsync(
                            exchange: deadLetterExchange);
                    }
                }
                catch
                {
                    // Não interfere na limpeza do PostgreSQL.
                }

                await publisherChannel.DisposeAsync();
            }

            if (publisherConnection is not null)
                await publisherConnection.DisposeAsync();

            if (serviceProvider is not null)
                await serviceProvider.DisposeAsync();

            await using var cleanupDb =
                new VehicleBillingDbContext(
                    new DbContextOptionsBuilder<VehicleBillingDbContext>()
                        .UseNpgsql(testConnectionString!)
                        .Options);

            await cleanupDb.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM inbox_messages WHERE id = {messageId}");
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

        public IReadOnlyCollection<string> Messages =>
            _messages.ToArray();

        public ILogger CreateLogger(string categoryName) =>
            new TestLogger(_messages);

        public void Dispose()
        {
        }

        public async Task WaitForAsync(
            Func<string, bool> condition,
            CancellationToken cancellationToken)
        {
            while (!Messages.Any(condition))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(100),
                    cancellationToken);
            }
        }

        public async Task WaitForCountAsync(
            Func<IReadOnlyCollection<string>, bool> condition,
            CancellationToken cancellationToken)
        {
            while (!condition(Messages))
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

                messages.Enqueue(formatter(state, exception));
            }
        }
    }
}
