using Comven_VehicleBilling.Infrastructure.Inbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

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
    {
      await ProcessMessageAsync(args, stoppingToken);
    };

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
    {
      // Encerramento normal do Worker.
    }
  }

  private async Task ProcessMessageAsync(
      BasicDeliverEventArgs args,
      CancellationToken cancellationToken)
  {
    if (_channel is null)
    {
      return;
    }

    var messageId = GetMessageId(args);

    if (messageId == Guid.Empty)
    {
      _logger.LogError(
          "Mensagem recebida sem MessageId válido. " +
          "DeliveryTag={DeliveryTag}.",
          args.DeliveryTag);

      await _channel.BasicNackAsync(
          deliveryTag: args.DeliveryTag,
          multiple: false,
          requeue: false,
          cancellationToken: cancellationToken);

      return;
    }

    try
    {
      using var scope =
          _scopeFactory.CreateScope();

      var dbContext =
          scope.ServiceProvider
              .GetRequiredService<
                  Infrastructure.Persistence.VehicleBillingDbContext>();

      var inboxRepository =
          scope.ServiceProvider
              .GetRequiredService<InboxRepository>();

      await using var transaction =
          await dbContext.Database.BeginTransactionAsync(
              cancellationToken);

      var alreadyProcessed =
          await inboxRepository.ExistsAsync(
              messageId,
              ConsumerName,
              cancellationToken);

      if (alreadyProcessed)
      {
        await transaction.CommitAsync(
            cancellationToken);

        await _channel.BasicAckAsync(
            deliveryTag: args.DeliveryTag,
            multiple: false,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Mensagem {MessageId} já processada pelo consumidor " +
            "{ConsumerName}. ACK realizado.",
            messageId,
            ConsumerName);

        return;
      }

      var payload =
          Encoding.UTF8.GetString(args.Body.ToArray());

      _logger.LogInformation(
          "Processando mensagem {MessageId}. Type={MessageType}.",
          messageId,
          args.BasicProperties.Type);

      // O processamento específico do evento entra aqui.
      //
      // Neste primeiro passo estamos estabelecendo o mecanismo
      // transacional de Inbox + processamento + ACK.
      //
      // O handler de OperationCreated será conectado aqui.

      await inboxRepository.AddAsync(
          messageId,
          ConsumerName,
          cancellationToken);

      await dbContext.SaveChangesAsync(
          cancellationToken);

      await transaction.CommitAsync(
          cancellationToken);

      await _channel.BasicAckAsync(
          deliveryTag: args.DeliveryTag,
          multiple: false,
          cancellationToken: cancellationToken);

      _logger.LogInformation(
          "Mensagem {MessageId} processada e confirmada.",
          messageId);
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Erro ao processar mensagem {MessageId}. " +
          "A mensagem será devolvida à fila.",
          messageId);

      await _channel.BasicNackAsync(
          deliveryTag: args.DeliveryTag,
          multiple: false,
          requeue: true,
          cancellationToken: cancellationToken);
    }
  }

  private static Guid GetMessageId(
      BasicDeliverEventArgs args)
  {
    var messageId =
        args.BasicProperties.MessageId;

    return Guid.TryParse(
        messageId,
        out var id)
        ? id
        : Guid.Empty;
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
          "RABBITMQ_HOST e RABBITMQ_VHOST nas variáveis " +
          "de ambiente.");
    }

    var port =
        int.TryParse(
            portValue,
            out var parsedPort)
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
      NetworkRecoveryInterval =
            TimeSpan.FromSeconds(10)
    };

    _connection =
        await factory.CreateConnectionAsync(
            cancellationToken);

    var options =
        new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: 1);

    _channel =
        await _connection.CreateChannelAsync(
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
        "Conexão do Consumer estabelecida. " +
        "Fila: {QueueName}.",
        QueueName);
  }

  public override async Task StopAsync(
      CancellationToken cancellationToken)
  {
    if (_channel is not null)
    {
      await _channel.CloseAsync(
          cancellationToken);
    }

    if (_connection is not null)
    {
      await _connection.CloseAsync(
          cancellationToken);
    }

    await base.StopAsync(
        cancellationToken);
  }
}
