using System.Text.Json;
using Comven_VehicleBilling.Infrastructure.Inbox;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.WorkerService.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Comven_VehicleBilling.WorkerService;

public sealed class RabbitMqConsumer : BackgroundService
{
  private const string QueueName = "vehicle-billing-events";
  private const string ConsumerName = "vehicle-billing-worker";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<RabbitMqConsumer> _logger;

  private IConnection? _connection;
  private IChannel? _channel;

  public RabbitMqConsumer(
      IServiceScopeFactory scopeFactory,
      ILogger<RabbitMqConsumer> logger)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(
      CancellationToken stoppingToken)
  {
    await ConnectAsync(stoppingToken);

    var consumer = new AsyncEventingBasicConsumer(_channel!);

    consumer.ReceivedAsync += async (_, args) =>
        await ProcessMessageAsync(args, stoppingToken);

    await _channel!.BasicConsumeAsync(
        queue: QueueName,
        autoAck: false,
        consumer: consumer,
        cancellationToken: stoppingToken);

    _logger.LogInformation(
        "Consumer {ConsumerName} aguardando mensagens na fila {QueueName}.",
        ConsumerName,
        QueueName);

    try
    {
      await Task.Delay(
          Timeout.InfiniteTimeSpan,
          stoppingToken);
    }
    catch (OperationCanceledException)
        when (stoppingToken.IsCancellationRequested)
    {
      // Mensagens sem ACK serão reentregues pelo RabbitMQ.
    }
  }

  private async Task ProcessMessageAsync(
      BasicDeliverEventArgs args,
      CancellationToken cancellationToken)
  {
    var channel = _channel;

    if (channel is null || !channel.IsOpen)
      return;

    var messageId = GetMessageId(args);

    if (messageId == Guid.Empty)
    {
      _logger.LogError(
          "Mensagem sem MessageId GUID válido. DeliveryTag={DeliveryTag}.",
          args.DeliveryTag);

      await RejectMessageAsync(
          channel,
          args.DeliveryTag);

      return;
    }

    try
    {
      ValidateEventId(args.Body, messageId);

      await using var scope =
          _scopeFactory.CreateAsyncScope();

      var services = scope.ServiceProvider;

      var dbContext =
          services.GetRequiredService<VehicleBillingDbContext>();

      var inbox =
          services.GetRequiredService<InboxRepository>();

      var createdHandler =
          services.GetRequiredService<OperationCreatedHandler>();

      var refundedHandler =
          services.GetRequiredService<OperationRefundedHandler>();

      await using var transaction =
          await dbContext.Database.BeginTransactionAsync(
              cancellationToken);

      var registered = await inbox.TryRegisterAsync(
          messageId,
          ConsumerName,
          cancellationToken);

      if (!registered)
      {
        await transaction.CommitAsync(cancellationToken);

        await channel.BasicAckAsync(
            args.DeliveryTag,
            multiple: false,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Mensagem {MessageId} já consta na Inbox. ACK realizado.",
            messageId);

        return;
      }

      var messageType = args.BasicProperties.Type;

      switch (messageType)
      {
        case "OperationCreated.v1":
          await createdHandler.HandleAsync(
              args.Body,
              cancellationToken);
          break;

        case "OperationRefunded.v1":
          await refundedHandler.HandleAsync(
              args.Body,
              cancellationToken);
          break;

        default:
          throw new PermanentMessageException(
              $"Tipo de mensagem não suportado: {messageType ?? "(vazio)"}.");
      }

      // Persiste a Inbox e as alterações dos handlers
      // na mesma transação PostgreSQL.
      await dbContext.SaveChangesAsync(cancellationToken);

      // Primeiro confirma a transação no PostgreSQL.
      await transaction.CommitAsync(cancellationToken);

      // Somente depois confirma a mensagem no RabbitMQ.
      await channel.BasicAckAsync(
          args.DeliveryTag,
          multiple: false,
          cancellationToken: cancellationToken);

      _logger.LogInformation(
          "Mensagem {MessageId} processada, persistida na Inbox e confirmada.",
          messageId);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
      // Não confirmar.
      // O RabbitMQ poderá reentregar a mensagem.
    }
    catch (PermanentMessageException exception)
    {
      _logger.LogError(
          exception,
          "Mensagem {MessageId} rejeitada por conteúdo inválido.",
          messageId);

      await RejectMessageAsync(
          channel,
          args.DeliveryTag);
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha transitória ao processar {MessageId}; mensagem será reentregue.",
          messageId);

      if (channel.IsOpen)
      {
        await channel.BasicNackAsync(
            args.DeliveryTag,
            multiple: false,
            requeue: true,
            cancellationToken: CancellationToken.None);
      }
    }
  }

  private static void ValidateEventId(
      ReadOnlyMemory<byte> body,
      Guid messageId)
  {
    try
    {
      using var document = JsonDocument.Parse(body);

      if (!document.RootElement.TryGetProperty(
              "EventId",
              out var eventIdProperty))
      {
        throw new PermanentMessageException(
            "Payload não contém EventId.");
      }

      if (eventIdProperty.ValueKind != JsonValueKind.String ||
          !Guid.TryParse(
              eventIdProperty.GetString(),
              out var eventId))
      {
        throw new PermanentMessageException(
            "EventId do payload não é um GUID válido.");
      }

      if (eventId != messageId)
      {
        throw new PermanentMessageException(
            "EventId do payload diverge do MessageId do RabbitMQ.");
      }
    }
    catch (JsonException exception)
    {
      throw new PermanentMessageException(
          "Payload JSON inválido.",
          exception);
    }
  }

  private static Guid GetMessageId(
      BasicDeliverEventArgs args)
  {
    var messageId = args.BasicProperties.MessageId;

    return Guid.TryParse(messageId, out var id)
        ? id
        : Guid.Empty;
  }

  private static async Task RejectMessageAsync(
      IChannel channel,
      ulong deliveryTag)
  {
    if (!channel.IsOpen)
      return;

    await channel.BasicNackAsync(
        deliveryTag,
        multiple: false,
        requeue: false,
        cancellationToken: CancellationToken.None);
  }

  private async Task ConnectAsync(
      CancellationToken cancellationToken)
  {
    var user =
        Environment.GetEnvironmentVariable("RABBITMQ_USER");

    var password =
        Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD");

    var host =
        Environment.GetEnvironmentVariable("RABBITMQ_HOST");

    var virtualHost =
        Environment.GetEnvironmentVariable("RABBITMQ_VHOST");

    var portValue =
        Environment.GetEnvironmentVariable("RABBITMQ_PORT");

    if (string.IsNullOrWhiteSpace(user) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(host) ||
        string.IsNullOrWhiteSpace(virtualHost))
    {
      throw new InvalidOperationException(
          "Configure RABBITMQ_USER, RABBITMQ_PASSWORD, " +
          "RABBITMQ_HOST e RABBITMQ_VHOST nas variáveis de ambiente.");
    }

    var port = int.TryParse(portValue, out var parsedPort)
        ? parsedPort
        : 5672;

    var factory = new ConnectionFactory
    {
      HostName = host,
      Port = port,
      UserName = user,
      Password = password,
      VirtualHost = virtualHost,
      AutomaticRecoveryEnabled = true,
      NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
    };

    _connection = await factory.CreateConnectionAsync(
        cancellationToken);

    var options = new CreateChannelOptions(
        publisherConfirmationsEnabled: false,
        publisherConfirmationTrackingEnabled: false,
        consumerDispatchConcurrency: 1);

    _channel = await _connection.CreateChannelAsync(
        options,
        cancellationToken);

    await _channel.QueueDeclareAsync(
        queue: QueueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null,
        cancellationToken: cancellationToken);

    await _channel.BasicQosAsync(
        prefetchSize: 0,
        prefetchCount: 1,
        global: false,
        cancellationToken: cancellationToken);

    _logger.LogInformation(
        "Conexão RabbitMQ estabelecida. Fila: {QueueName}.",
        QueueName);
  }

  public override async Task StopAsync(
      CancellationToken cancellationToken)
  {
    await base.StopAsync(cancellationToken);

    if (_channel is not null && _channel.IsOpen)
      await _channel.CloseAsync(cancellationToken);

    if (_connection is not null && _connection.IsOpen)
      await _connection.CloseAsync(cancellationToken);
  }

  public override void Dispose()
  {
    _channel?.Dispose();
    _connection?.Dispose();

    base.Dispose();
  }
}
