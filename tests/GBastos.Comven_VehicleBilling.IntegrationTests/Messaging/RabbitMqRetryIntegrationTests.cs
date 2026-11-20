using System.Collections.Concurrent;
using System.Text;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.WorkerService;
using Comven_VehicleBilling.WorkerService.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Comven_VehicleBilling.IntegrationTests.Messaging;

[Collection("PostgreSQL integration tests")]
public sealed class RabbitMqRetryIntegrationTests
{
    [Fact]
    public async Task ShouldPublishFirstRetryPreservingMessagePropertiesAndHeaders()
    {
        var hostName = RequiredEnvironmentVariable("RABBITMQ_HOST");
        var userName = RequiredEnvironmentVariable("RABBITMQ_USER");
        var password = RequiredEnvironmentVariable("RABBITMQ_PASSWORD");
        var virtualHost = RequiredEnvironmentVariable("RABBITMQ_VHOST");

        var testConnectionString =
            RequiredEnvironmentVariable("VEHICLE_BILLING_TEST_CONNECTION_STRING");

        var portText = Environment.GetEnvironmentVariable("RABBITMQ_PORT");

        var port = int.TryParse(portText, out var configuredPort)
            ? configuredPort
            : 5672;

        var suffix = Guid.NewGuid().ToString("N");

        var queueName = $"vehicle-billing.retry.integration.{suffix}";
        var retryQueue = $"{queueName}.retry.30s";
        var deadLetterExchange = $"{queueName}.dlx";
        var deadLetterQueue = $"{queueName}.dlq";

        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString("N");

        var body = Encoding.UTF8.GetBytes(
            $$"""{"EventId":"{{messageId}}","OperationId":"{{Guid.NewGuid()}}"}""");

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(30));

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
                ConsumerName = $"retry-consumer-{suffix}",
                DeadLetterExchange = deadLetterExchange,
                DeadLetterQueue = deadLetterQueue,
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
                    new RetryQueueOptions(retryQueue, 30_000)
                ]
            };

            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddProvider(logProvider);
            });

            services.AddInfrastructure(configuration);

            services.AddScoped<
                IOperationCreatedHandler,
                AlwaysFailingOperationCreatedHandler>();

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

            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"Fila de retry {retryQueue}",
                    StringComparison.Ordinal),
                timeout.Token);

            var properties = new BasicProperties
            {
                MessageId = messageId.ToString(),
                CorrelationId = correlationId,
                Type = "OperationCreated.v1",
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?>
                {
                    ["x-custom-header"] = "preserve-me",
                    ["x-retry-count"] = 0
                }
            };

            await publisherChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: timeout.Token);

            // Aguarda a publicação do retry e o ACK da mensagem original.
            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"Mensagem {messageId} encaminhada para {retryQueue}",
                    StringComparison.Ordinal),
                timeout.Token);

            // Recupera a mensagem diretamente da fila de retry.
            BasicGetResult? retryMessage = null;

            while (retryMessage is null)
            {
                timeout.Token.ThrowIfCancellationRequested();

                retryMessage = await publisherChannel.BasicGetAsync(
                    retryQueue,
                    autoAck: false,
                    cancellationToken: timeout.Token);

                if (retryMessage is null)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(100),
                        timeout.Token);
                }
            }

            var receivedProperties = retryMessage.BasicProperties;

            Assert.Equal(
                messageId.ToString(),
                receivedProperties.MessageId);

            Assert.Equal(
                correlationId,
                receivedProperties.CorrelationId);

            Assert.Equal(
                "OperationCreated.v1",
                receivedProperties.Type);

            Assert.Equal(
                "application/json",
                receivedProperties.ContentType);

            Assert.Equal(
                DeliveryModes.Persistent,
                receivedProperties.DeliveryMode);

            Assert.Equal(
                body,
                retryMessage.Body.ToArray());

            var headers = receivedProperties.Headers;

            Assert.NotNull(headers);

            Assert.True(
                headers.TryGetValue(
                    "x-retry-count",
                    out var retryCount));

            Assert.Equal(1, Convert.ToInt32(retryCount));

            Assert.True(
                headers.TryGetValue(
                    "x-custom-header",
                    out var customHeader));

            Assert.Equal(
                "preserve-me",
                HeaderAsString(customHeader));

            // Confirma o recebimento da mensagem de retry pelo teste.
            await publisherChannel.BasicAckAsync(
                retryMessage.DeliveryTag,
                multiple: false,
                cancellationToken: timeout.Token);
        }
        finally
        {
            if (serviceProvider is not null)
            {
                try
                {
                    var consumer =
                        serviceProvider.GetRequiredService<RabbitMqConsumer>();

                    await consumer.StopAsync(
                        new CancellationTokenSource(
                            TimeSpan.FromSeconds(10)).Token);
                }
                catch
                {
                    // Continua a limpeza dos recursos exclusivos.
                }
            }

            if (publisherChannel is not null)
            {
                try
                {
                    if (publisherChannel.IsOpen)
                    {
                        await publisherChannel.QueueDeleteAsync(queueName);
                        await publisherChannel.QueueDeleteAsync(retryQueue);
                        await publisherChannel.QueueDeleteAsync(deadLetterQueue);

                        await publisherChannel.ExchangeDeleteAsync(
                            deadLetterExchange);
                    }
                }
                catch
                {
                    // Não interfere no encerramento dos demais recursos.
                }

                await publisherChannel.DisposeAsync();
            }

            if (publisherConnection is not null)
                await publisherConnection.DisposeAsync();

            if (serviceProvider is not null)
                await serviceProvider.DisposeAsync();
        }
    }

    [Fact]
    public async Task ShouldSendMessageToDeadLetterQueueAfterMaxRetryAttempts()
    {
        var hostName = RequiredEnvironmentVariable("RABBITMQ_HOST");
        var userName = RequiredEnvironmentVariable("RABBITMQ_USER");
        var password = RequiredEnvironmentVariable("RABBITMQ_PASSWORD");
        var virtualHost = RequiredEnvironmentVariable("RABBITMQ_VHOST");

        var testConnectionString =
            RequiredEnvironmentVariable("VEHICLE_BILLING_TEST_CONNECTION_STRING");

        var portText = Environment.GetEnvironmentVariable("RABBITMQ_PORT");

        var port = int.TryParse(portText, out var configuredPort)
            ? configuredPort
            : 5672;

        var suffix = Guid.NewGuid().ToString("N");

        var queueName = $"vehicle-billing.dlq.integration.{suffix}";

        var retryQueues = new[]
        {
            new RetryQueueOptions($"{queueName}.retry.1s", 1_000),
            new RetryQueueOptions($"{queueName}.retry.2s", 2_000),
            new RetryQueueOptions($"{queueName}.retry.3s", 3_000)
        };

        var retryQueueNames = retryQueues
            .Select(retry => retry.QueueName)
            .ToArray();

        var deadLetterExchange = $"{queueName}.dlx";
        var deadLetterQueue = $"{queueName}.dlq";

        var messageId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString("N");

        var body = Encoding.UTF8.GetBytes(
            $$"""{"EventId":"{{messageId}}","OperationId":"{{operationId}}"}""");

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(45));

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
                ConsumerName = $"dlq-consumer-{suffix}",
                DeadLetterExchange = deadLetterExchange,
                DeadLetterQueue = deadLetterQueue,
                HostName = hostName,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                Port = port,
                ReconnectIntervalSeconds = 1,
                MaxRetryAttempts = 3,
                RetryHeader = "x-retry-count",
                RetryQueues = retryQueues
            };

            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddProvider(logProvider);
            });

            services.AddInfrastructure(configuration);

            services.AddScoped<
                IOperationCreatedHandler,
                AlwaysFailingOperationCreatedHandler>();

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

            // Garante que todas as filas de retry foram declaradas.
            foreach (var retryQueueName in retryQueueNames)
            {
                await logProvider.WaitForAsync(
                    log => log.Contains(
                        $"Fila de retry {retryQueueName}",
                        StringComparison.Ordinal),
                    timeout.Token);
            }

            var properties = new BasicProperties
            {
                MessageId = messageId.ToString(),
                CorrelationId = correlationId,
                Type = "OperationCreated.v1",
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?>
                {
                    ["x-custom-header"] = "preserve-me",
                    ["x-retry-count"] = 0
                }
            };

            await publisherChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: timeout.Token);

            // Aguarda o consumidor esgotar as três tentativas.
            await logProvider.WaitForAsync(
                log => log.Contains(
                    $"Mensagem {messageId} esgotou as 3 tentativas",
                    StringComparison.Ordinal),
                timeout.Token);

            // O NACK pode ocorrer imediatamente após o log.
            // Aguarda a mensagem chegar à DLQ pelo Dead Letter Exchange.
            BasicGetResult? deadLetterMessage = null;

            while (deadLetterMessage is null)
            {
                timeout.Token.ThrowIfCancellationRequested();

                deadLetterMessage = await publisherChannel.BasicGetAsync(
                    deadLetterQueue,
                    autoAck: false,
                    cancellationToken: timeout.Token);

                if (deadLetterMessage is null)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(100),
                        timeout.Token);
                }
            }

            var receivedProperties =
                deadLetterMessage.BasicProperties;

            Assert.Equal(
                messageId.ToString(),
                receivedProperties.MessageId);

            Assert.Equal(
                correlationId,
                receivedProperties.CorrelationId);

            Assert.Equal(
                "OperationCreated.v1",
                receivedProperties.Type);

            Assert.Equal(
                "application/json",
                receivedProperties.ContentType);

            Assert.Equal(
                DeliveryModes.Persistent,
                receivedProperties.DeliveryMode);

            Assert.Equal(
                body,
                deadLetterMessage.Body.ToArray());

            var headers = receivedProperties.Headers;

            Assert.NotNull(headers);

            Assert.True(
                headers.TryGetValue(
                    "x-retry-count",
                    out var retryCount));

            Assert.Equal(3, Convert.ToInt32(retryCount));

            Assert.True(
                headers.TryGetValue(
                    "x-custom-header",
                    out var customHeader));

            Assert.Equal(
                "preserve-me",
                HeaderAsString(customHeader));

            // Confirma o recebimento da mensagem na DLQ.
            await publisherChannel.BasicAckAsync(
                deadLetterMessage.DeliveryTag,
                multiple: false,
                cancellationToken: timeout.Token);
        }
        finally
        {
            if (serviceProvider is not null)
            {
                try
                {
                    var consumer =
                        serviceProvider.GetRequiredService<RabbitMqConsumer>();

                    using var stopTimeout =
                        new CancellationTokenSource(
                            TimeSpan.FromSeconds(10));

                    await consumer.StopAsync(stopTimeout.Token);
                }
                catch
                {
                    // Continua a limpeza dos recursos exclusivos.
                }
            }

            if (publisherChannel is not null)
            {
                try
                {
                    if (publisherChannel.IsOpen)
                    {
                        await publisherChannel.QueueDeleteAsync(queueName);

                        foreach (var retryQueueName in retryQueueNames)
                        {
                            await publisherChannel.QueueDeleteAsync(
                                retryQueueName);
                        }

                        await publisherChannel.QueueDeleteAsync(
                            deadLetterQueue);

                        await publisherChannel.ExchangeDeleteAsync(
                            deadLetterExchange);
                    }
                }
                catch
                {
                    // Não interfere no encerramento dos demais recursos.
                }

                await publisherChannel.DisposeAsync();
            }

            if (publisherConnection is not null)
                await publisherConnection.DisposeAsync();

            if (serviceProvider is not null)
                await serviceProvider.DisposeAsync();
        }
    }
    private static string HeaderAsString(object? value)
    {
        return value switch
        {
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory =>
                Encoding.UTF8.GetString(memory.Span),
            _ => Convert.ToString(value) ?? string.Empty
        };
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

    private sealed class AlwaysFailingOperationCreatedHandler
        : IOperationCreatedHandler
    {
        public Task HandleAsync(
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Falha transitória simulada pelo teste de retry.");
        }
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public ILogger CreateLogger(string categoryName) =>
            new TestLogger(_messages);

        public void Dispose()
        {
        }

        public async Task WaitForAsync(
            Func<string, bool> condition,
            CancellationToken cancellationToken)
        {
            while (! _messages.Any(condition))
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
                if (IsEnabled(logLevel))
                {
                    var formattedMessage = formatter(state, exception);
                    messages.Enqueue(formattedMessage);
                    Console.WriteLine($"[RABBITMQ TEST] {formattedMessage}");

                    if (exception is not null)
                        Console.WriteLine(exception);
                }
            }
        }
    }
}





