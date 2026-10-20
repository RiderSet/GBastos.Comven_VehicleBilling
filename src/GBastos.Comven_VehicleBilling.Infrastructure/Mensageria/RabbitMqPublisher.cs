using Comven_VehicleBilling.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;

namespace Comven_VehicleBilling.Infrastructure.Mensageria;

public sealed class RabbitMqPublisher : IAsyncDisposable
{
  private const string QueueName = "vehicle-billing-events";

  private readonly ILogger<RabbitMqPublisher> _logger;
  private readonly SemaphoreSlim _publishLock = new(1, 1);

  private IConnection? _connection;
  private IChannel? _channel;

  public RabbitMqPublisher(ILogger<RabbitMqPublisher> logger)
  {
    _logger = logger;
  }

  public async Task PublishAsync(
      OutboxMessage message,
      CancellationToken cancellationToken = default)
  {
    await _publishLock.WaitAsync(cancellationToken);

    try
    {
      await EnsureChannelAsync(cancellationToken);

      var properties = new BasicProperties
      {
        MessageId = message.Id.ToString(),
        Type = message.MessageType,
        ContentType = "application/json",
        Persistent = true
      };

      var body = Encoding.UTF8.GetBytes(message.Payload);

      await _channel!.BasicPublishAsync(
          exchange: string.Empty,
          routingKey: QueueName,
          mandatory: true,
          basicProperties: properties,
          body: body,
          cancellationToken: cancellationToken);

      _logger.LogInformation(
          "Mensagem {MessageId} publicada na fila {QueueName}.",
          message.Id,
          QueueName);
    }
    finally
    {
      _publishLock.Release();
    }
  }

  private async Task EnsureChannelAsync(
      CancellationToken cancellationToken)
  {
    if (_connection is { IsOpen: true } &&
        _channel is { IsOpen: true })
    {
      return;
    }

    if (_channel is not null)
    {
      await _channel.DisposeAsync();
      _channel = null;
    }

    if (_connection is not { IsOpen: true })
    {
      if (_connection is not null)
      {
        await _connection.DisposeAsync();
        _connection = null;
      }

      var user = Environment.GetEnvironmentVariable("RABBITMQ_USER");
      var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD");
      var virtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST");

      if (string.IsNullOrWhiteSpace(user) ||
          string.IsNullOrWhiteSpace(password) ||
          string.IsNullOrWhiteSpace(virtualHost))
      {
        throw new InvalidOperationException(
            "Configure RABBITMQ_USER, RABBITMQ_PASSWORD " +
            "e RABBITMQ_VHOST nas variáveis de ambiente.");
      }

      var factory = new ConnectionFactory
      {
        HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST")
              ?? "localhost",
        Port = int.TryParse(
              Environment.GetEnvironmentVariable("RABBITMQ_PORT"),
              out var port) ? port : 5672,
        UserName = user,
        Password = password,
        VirtualHost = virtualHost,
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
      };

      Console.WriteLine(
          $"RABBITMQ_DIAG Host={factory.HostName} " +
          $"Port={factory.Port} " +
          $"User={factory.UserName} " +
          $"VHost={factory.VirtualHost} " +
          $"PasswordLength={password?.Length ?? 0}");

      _connection = await factory.CreateConnectionAsync(

          cancellationToken);
    }

    var options = new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    _channel = await _connection!.CreateChannelAsync(
        options,
        cancellationToken);

    await _channel.QueueDeclareAsync(
        queue: QueueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null,
        cancellationToken: cancellationToken);

    _logger.LogInformation(
        "Conexão RabbitMQ estabelecida. Fila: {QueueName}.",
        QueueName);
  }

  public async ValueTask DisposeAsync()
  {
    if (_channel is not null)
      await _channel.DisposeAsync();

    if (_connection is not null)
      await _connection.DisposeAsync();

    _publishLock.Dispose();
  }
}
