using Microsoft.Extensions.Configuration;

namespace Comven_VehicleBilling.WorkerService;

public sealed class RabbitMqConsumerOptions
{
    public string QueueName { get; init; } = "vehicle-billing-events";

    public string ConsumerName { get; init; } = "vehicle-billing-worker";

    public int MaxRetryAttempts { get; init; } = 3;

    public string RetryHeader { get; init; } = "x-retry-count";

    public string DeadLetterExchange { get; init; } = "vehicle-billing.dlx";

    public string DeadLetterQueue { get; init; } = "vehicle-billing-events.dlq";

    public string? HostName { get; init; }

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public string? VirtualHost { get; init; }

    public int Port { get; init; } = 5672;

    public int ReconnectIntervalSeconds { get; init; } = 10;

    public IReadOnlyList<RetryQueueOptions> RetryQueues { get; init; } =
    [
        new("vehicle-billing-events.retry.5s", 5_000),
        new("vehicle-billing-events.retry.30s", 30_000),
        new("vehicle-billing-events.retry.120s", 120_000)
    ];

    public static RabbitMqConsumerOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var queueName = GetString(
            configuration, "RABBITMQ_QUEUE", "vehicle-billing-events");

        var retryIntervals = GetRetryIntervals(configuration);

        var maxRetryAttempts = GetPositiveInt(
            configuration,
            "RABBITMQ_MAX_RETRY_ATTEMPTS",
            3);

        if (maxRetryAttempts > retryIntervals.Count)
        {
            throw new InvalidOperationException(
                "RABBITMQ_MAX_RETRY_ATTEMPTS não pode ser maior " +
                "que a quantidade de intervalos de retry configurados.");
        }

        var retryQueues = retryIntervals
            .Select(seconds => new RetryQueueOptions(
                $"{queueName}.retry.{seconds}s",
                checked(seconds * 1000)))
            .ToArray();

        return new RabbitMqConsumerOptions
        {
            QueueName = queueName,

            ConsumerName = GetString(
                configuration,
                "RABBITMQ_CONSUMER_NAME",
                "vehicle-billing-worker"),

            MaxRetryAttempts = maxRetryAttempts,

            RetryHeader = GetString(
                configuration,
                "RABBITMQ_RETRY_HEADER",
                "x-retry-count"),

            DeadLetterExchange = GetString(
                configuration,
                "RABBITMQ_DEAD_LETTER_EXCHANGE",
                "vehicle-billing.dlx"),

            DeadLetterQueue = GetString(
                configuration,
                "RABBITMQ_DEAD_LETTER_QUEUE",
                "vehicle-billing-events.dlq"),

            HostName = configuration["RABBITMQ_HOST"],
            UserName = configuration["RABBITMQ_USER"],
            Password = configuration["RABBITMQ_PASSWORD"],
            VirtualHost = configuration["RABBITMQ_VHOST"],

            Port = GetPositiveInt(
                configuration,
                "RABBITMQ_PORT",
                5672),

            ReconnectIntervalSeconds = GetPositiveInt(
                configuration,
                "RABBITMQ_RECONNECT_INTERVAL_SECONDS",
                10),

            RetryQueues = retryQueues
        };
    }

    private static string GetString(
        IConfiguration configuration,
        string key,
        string defaultValue)
    {
        var value = configuration[key];

        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value.Trim();
    }

    private static int GetPositiveInt(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (!int.TryParse(value, out var result) || result <= 0)
        {
            throw new InvalidOperationException(
                $"{key} deve conter um número inteiro positivo.");
        }

        return result;
    }

    private static IReadOnlyList<int> GetRetryIntervals(
        IConfiguration configuration)
    {
        var value = configuration["RABBITMQ_RETRY_INTERVALS_SECONDS"];

        if (string.IsNullOrWhiteSpace(value))
            return [5, 30, 120];

        var intervals = new List<int>();

        foreach (var item in value.Split(','))
        {
            if (!int.TryParse(item.Trim(), out var seconds) ||
                seconds <= 0 ||
                seconds > int.MaxValue / 1000)
            {
                throw new InvalidOperationException(
                    "RABBITMQ_RETRY_INTERVALS_SECONDS contém " +
                    "um intervalo inválido.");
            }

            intervals.Add(seconds);
        }

        if (intervals.Count == 0 ||
            intervals.Distinct().Count() != intervals.Count)
        {
            throw new InvalidOperationException(
                "Os intervalos de retry devem ser positivos e únicos.");
        }

        return intervals;
    }
}

public sealed record RetryQueueOptions(
    string QueueName,
    int TtlMilliseconds);
