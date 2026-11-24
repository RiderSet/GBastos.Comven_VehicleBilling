using System.Text;
using Comven_VehicleBilling.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Comven_VehicleBilling.Infrastructure.Mensageria;

public sealed class RabbitMqPublisher : IAsyncDisposable
{
  private readonly ILogger<RabbitMqPublisher> _logger;
  private readonly ConnectionFactory _connectionFactory;
  private readonly string _queueName;
  private readonly string _paymentQueueName;
  private readonly string _deadLetterExchange;
  private readonly SemaphoreSlim _publishLock = new(1, 1);

  private IConnection? _connection;
  private IChannel? _channel;

  public RabbitMqPublisher(
      ILogger<RabbitMqPublisher> logger,
      IConfiguration configuration)
  {
    _logger = logger;

    _queueName =
        configuration["RABBITMQ_QUEUE"]
        ?? "vehicle-billing-events";

    _paymentQueueName =
        configuration["RABBITMQ_PAYMENT_QUEUE"]
        ?? "vehicle-billing-payment-events";

    if (string.Equals(
            _queueName,
            _paymentQueueName,
            StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException(
          "A fila de pagamentos deve ser diferente da fila principal.");
    }

    _deadLetterExchange =
        configuration["RABBITMQ_DEAD_LETTER_EXCHANGE"]
        ?? "vehicle-billing.dlx";

    var user = configuration["RABBITMQ_USER"];
    var password = configuration["RABBITMQ_PASSWORD"];
    var virtualHost = configuration["RABBITMQ_VHOST"];
    var host = configuration["RABBITMQ_HOST"];
    var portValue = configuration["RABBITMQ_PORT"];

    if (string.IsNullOrWhiteSpace(user) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(virtualHost))
    {
      throw new InvalidOperationException(
          "Configure RABBITMQ_USER, RABBITMQ_PASSWORD " +
          "e RABBITMQ_VHOST nas variáveis de ambiente.");
    }

    var port = 5672;

    if (!string.IsNullOrWhiteSpace(portValue) &&
        (!int.TryParse(portValue, out port) ||
         port < 1 ||
         port > 65535))
    {
      throw new InvalidOperationException(
          "RABBITMQ_PORT deve conter uma porta válida.");
    }

    _connectionFactory = new ConnectionFactory
    {
      HostName = string.IsNullOrWhiteSpace(host)
            ? "localhost"
            : host,
      Port = port,
      UserName = user,
      Password = password,
      VirtualHost = virtualHost,
      AutomaticRecoveryEnabled = true,
      NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
    };
  }

  public async Task PublishAsync(
      OutboxMessage message,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);

    await _publishLock.WaitAsync(cancellationToken);

    try
    {
      await EnsureChannelAsync(cancellationToken);

      var targetQueue = GetQueueName(message.MessageType);

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
          routingKey: targetQueue,
          mandatory: true,
          basicProperties: properties,
          body: body,
          cancellationToken: cancellationToken);

      _logger.LogInformation(
          "Mensagem {MessageId} ({MessageType}) publicada e confirmada pelo RabbitMQ. Fila: {QueueName}.",
          message.Id,
          message.MessageType,
          targetQueue);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha ao publicar a mensagem {MessageId}.",
          message.Id);

      throw;
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

      _connection = await _connectionFactory.CreateConnectionAsync(
          cancellationToken);
    }

    var options = new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    _channel = await _connection!.CreateChannelAsync(
        options,
        cancellationToken);

    await _channel.QueueDeclareAsync(
        queue: _queueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: new Dictionary<string, object?>
        {
          ["x-dead-letter-exchange"] = _deadLetterExchange
        },
        cancellationToken: cancellationToken);

    _logger.LogInformation(
        "Conexão RabbitMQ estabelecida. Fila: {QueueName}.",
        _queueName);

    await _channel.QueueDeclareAsync(
    queue: _paymentQueueName,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: new Dictionary<string, object?>
    {
      ["x-dead-letter-exchange"] = _deadLetterExchange
    },
    cancellationToken: cancellationToken);

  }

  private string GetQueueName(string messageType)
  {
    return messageType switch
    {
      "InvoicePaid.v1" =>
          _paymentQueueName,

      "InvoicePaymentFailed.v1" =>
          _paymentQueueName,

      "InvoicePaymentUnknown.v1" =>
          _paymentQueueName,

      _ => _queueName
    };
  }

  public async ValueTask DisposeAsync()
  {
    if (_channel is not null)
    {
      await _channel.DisposeAsync();
      _channel = null;
    }

    if (_connection is not null)
    {
      await _connection.DisposeAsync();
      _connection = null;
    }

    _publishLock.Dispose();
  }
}
