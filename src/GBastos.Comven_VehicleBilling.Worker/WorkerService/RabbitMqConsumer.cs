using Comven_VehicleBilling.Infrastructure.Inbox;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Comven_VehicleBilling.WorkerService.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Comven_VehicleBilling.WorkerService;

public sealed class RabbitMqConsumer : BackgroundService
{
  private const string QueueName = "vehicle-billing-events";
  private const string ConsumerName = "vehicle-billing-worker";

  private const int MaxRetryAttempts = 3;
  private const string RetryHeader = "x-retry-count";

  private const string DeadLetterExchange = "vehicle-billing.dlx";
  private const string DeadLetterQueue = "vehicle-billing-events.dlq";

  private static readonly TimeSpan ReconnectInterval =
      TimeSpan.FromSeconds(10);

  private static readonly (string QueueName, int TtlMilliseconds)[]
      RetryQueues =
      [
          ("vehicle-billing-events.retry.5s", 5_000),
            ("vehicle-billing-events.retry.30s", 30_000),
            ("vehicle-billing-events.retry.120s", 120_000)
      ];

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<RabbitMqConsumer> _logger;

  private IConnection? _connection;
  private IChannel? _consumerChannel;
  private IChannel? _publisherChannel;

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
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await ConnectAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(
            _consumerChannel!);

        consumer.ReceivedAsync += async (_, args) =>
            await ProcessMessageAsync(args, stoppingToken);

        await _consumerChannel!.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Consumer {ConsumerName} aguardando mensagens na fila {QueueName}.",
            ConsumerName,
            QueueName);

        // Supervisiona a conexão. Se ela cair, sai do ciclo
        // e tenta estabelecer uma nova conexão.
        while (!stoppingToken.IsCancellationRequested &&
               _connection?.IsOpen == true &&
               _consumerChannel?.IsOpen == true &&
               _publisherChannel?.IsOpen == true)
        {
          await Task.Delay(
              TimeSpan.FromSeconds(1),
              stoppingToken);
        }

        if (!stoppingToken.IsCancellationRequested)
        {
          _logger.LogWarning(
              "Conexão ou canal RabbitMQ indisponível. Será realizada uma nova conexão.");
        }
      }
      catch (OperationCanceledException)
          when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "Falha no Consumer RabbitMQ. Nova tentativa em {IntervalSeconds} segundos.",
            ReconnectInterval.TotalSeconds);
      }
      finally
      {
        await CloseConnectionAsync();
      }

      if (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          await Task.Delay(
              ReconnectInterval,
              stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
          break;
        }
      }
    }

    _logger.LogInformation(
        "Consumer RabbitMQ finalizado.");
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

      // A reconexão é controlada pelo ExecuteAsync.
      AutomaticRecoveryEnabled = false
    };

    _connection = await factory.CreateConnectionAsync(
        cancellationToken);

    // Canal exclusivo para consumo e ACK/NACK.
    _consumerChannel = await _connection.CreateChannelAsync(
        cancellationToken: cancellationToken);

    // Canal exclusivo para publicações com confirmação.
    var publisherOptions = new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    _publisherChannel = await _connection.CreateChannelAsync(
        publisherOptions,
        cancellationToken);

    await DeclareTopologyAsync(cancellationToken);

    await _consumerChannel.BasicQosAsync(
        prefetchSize: 0,
        prefetchCount: 1,
        global: false,
        cancellationToken: cancellationToken);

    _logger.LogInformation(
        "Conexão RabbitMQ estabelecida. Fila principal: {QueueName}.",
        QueueName);
  }

  private async Task DeclareTopologyAsync(
      CancellationToken cancellationToken)
  {
    var channel = _consumerChannel!;

    await channel.ExchangeDeclareAsync(
        exchange: DeadLetterExchange,
        type: ExchangeType.Fanout,
        durable: true,
        autoDelete: false,
        arguments: null,
        cancellationToken: cancellationToken);

    await channel.QueueDeclareAsync(
        queue: DeadLetterQueue,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null,
        cancellationToken: cancellationToken);

    await channel.QueueBindAsync(
        queue: DeadLetterQueue,
        exchange: DeadLetterExchange,
        routingKey: string.Empty,
        arguments: null,
        cancellationToken: cancellationToken);

    // A fila principal encaminha mensagens rejeitadas
    // sem requeue para a Dead Letter Exchange.
    var mainQueueArguments = new Dictionary<string, object?>
    {
      ["x-dead-letter-exchange"] = DeadLetterExchange
    };

    await channel.QueueDeclareAsync(
        queue: QueueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: mainQueueArguments,
        cancellationToken: cancellationToken);

    await DeclareRetryQueuesAsync(cancellationToken);
  }

  private async Task DeclareRetryQueuesAsync(
      CancellationToken cancellationToken)
  {
    foreach (var retry in RetryQueues)
    {
      var arguments = new Dictionary<string, object?>
      {
        ["x-message-ttl"] = retry.TtlMilliseconds,
        ["x-dead-letter-exchange"] = string.Empty,
        ["x-dead-letter-routing-key"] = QueueName
      };

      await _consumerChannel!.QueueDeclareAsync(
          queue: retry.QueueName,
          durable: true,
          exclusive: false,
          autoDelete: false,
          arguments: arguments,
          cancellationToken: cancellationToken);

      _logger.LogInformation(
          "Fila de retry {RetryQueue} declarada com TTL de {TtlSeconds}s.",
          retry.QueueName,
          retry.TtlMilliseconds / 1000);
    }
  }

  private async Task ProcessMessageAsync(
      BasicDeliverEventArgs args,
      CancellationToken cancellationToken)
  {
    var channel = _consumerChannel;

    if (channel is null || !channel.IsOpen)
      return;

    var messageId = GetMessageId(args);

    if (messageId == Guid.Empty)
    {
      _logger.LogError(
          "Mensagem sem MessageId GUID válido. DeliveryTag={DeliveryTag}.",
          args.DeliveryTag);

      await RejectMessageAsync(channel, args.DeliveryTag);
      return;
    }

    // FASE 1: processamento transacional no PostgreSQL.
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

      if (registered)
      {
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

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Mensagem {MessageId} processada. Preparando commit PostgreSQL.",
            messageId);
      }
      else
      {
        _logger.LogInformation(
            "Mensagem {MessageId} já consta na Inbox. Nenhum efeito será repetido.",
            messageId);
      }

      // Commit da transação antes de qualquer ACK.
      await transaction.CommitAsync(cancellationToken);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
      // Sem ACK: o RabbitMQ poderá reentregar a mensagem.
      return;
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

      return;
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha no processamento transacional de {MessageId}.",
          messageId);

      await HandleTransientFailureAsync(
          args,
          messageId);

      return;
    }

    // FASE 2: confirmação RabbitMQ.
    // Neste ponto, o commit PostgreSQL já foi concluído.
    try
    {
      if (!channel.IsOpen)
      {
        _logger.LogWarning(
            "PostgreSQL confirmou {MessageId}, mas o canal RabbitMQ está fechado. A mensagem poderá ser reentregue.",
            messageId);

        return;
      }

      await channel.BasicAckAsync(
          args.DeliveryTag,
          multiple: false,
          cancellationToken: CancellationToken.None);

      _logger.LogInformation(
          "Mensagem {MessageId} confirmada no RabbitMQ após commit PostgreSQL.",
          messageId);
    }
    catch (Exception exception)
    {
      // Não publicar retry aqui!
      // O PostgreSQL já confirmou a transação.
      // A Inbox protegerá contra efeitos duplicados na reentrega.
      _logger.LogError(
          exception,
          "Commit PostgreSQL concluído para {MessageId}, mas houve falha no ACK RabbitMQ. A mensagem poderá ser reentregue.",
          messageId);
    }
  }

  private async Task HandleTransientFailureAsync(
      BasicDeliverEventArgs args,
      Guid messageId)
  {
    var consumerChannel = _consumerChannel;
    var publisherChannel = _publisherChannel;

    if (consumerChannel is null ||
        publisherChannel is null ||
        !consumerChannel.IsOpen ||
        !publisherChannel.IsOpen)
    {
      return;
    }

    var retryCount = GetRetryCount(args);

    if (retryCount >= MaxRetryAttempts)
    {
      _logger.LogError(
          "Mensagem {MessageId} esgotou as {MaxAttempts} tentativas. Encaminhando para DLQ.",
          messageId,
          MaxRetryAttempts);

      await RejectMessageAsync(
          consumerChannel,
          args.DeliveryTag);

      return;
    }

    var retry = RetryQueues[retryCount];

    try
    {
      var headers = args.BasicProperties.Headers is { } originalHeaders
          ? new Dictionary<string, object?>(originalHeaders)
          : new Dictionary<string, object?>();

      headers[RetryHeader] = retryCount + 1;

      var properties = new BasicProperties
      {
        MessageId = args.BasicProperties.MessageId,
        Type = args.BasicProperties.Type,
        CorrelationId = args.BasicProperties.CorrelationId,
        ContentType = args.BasicProperties.ContentType,
        ContentEncoding = args.BasicProperties.ContentEncoding,
        DeliveryMode = DeliveryModes.Persistent,
        Headers = headers
      };

      // Publica no canal exclusivo, com Publisher Confirms.
      // mandatory=true evita descartar silenciosamente uma
      // mensagem sem rota; a confirmação é acompanhada pelo cliente.
      await publisherChannel.BasicPublishAsync(
          exchange: string.Empty,
          routingKey: retry.QueueName,
          mandatory: true,
          basicProperties: properties,
          body: args.Body,
          cancellationToken: CancellationToken.None);
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha ao publicar retry de {MessageId}. A mensagem original será reentregue.",
          messageId);

      if (consumerChannel.IsOpen)
      {
        await consumerChannel.BasicNackAsync(
            args.DeliveryTag,
            multiple: false,
            requeue: true,
            cancellationToken: CancellationToken.None);
      }

      return;
    }

    // Só confirma a mensagem original depois que a publicação
    // do retry foi confirmada pelo canal publicador.
    try
    {
      await consumerChannel.BasicAckAsync(
          args.DeliveryTag,
          multiple: false,
          cancellationToken: CancellationToken.None);

      _logger.LogWarning(
          "Mensagem {MessageId} encaminhada para {RetryQueue}. Tentativa {Attempt}/{MaxAttempts}. Intervalo: {DelaySeconds}s.",
          messageId,
          retry.QueueName,
          retryCount + 1,
          MaxRetryAttempts,
          retry.TtlMilliseconds / 1000);
    }
    catch (Exception exception)
    {
      // Se o ACK falhar, a mensagem original poderá ser
      // reentregue. A Inbox protege contra efeitos duplicados.
      _logger.LogError(
          exception,
          "Retry publicado para {MessageId}, mas não foi possível confirmar a mensagem original.",
          messageId);
    }
  }

  private int GetRetryCount(BasicDeliverEventArgs args)
  {
    var headers = args.BasicProperties.Headers;

    if (headers is null ||
        !headers.TryGetValue(RetryHeader, out var value))
    {
      return 0;
    }

    return value switch
    {
      byte number when number <= MaxRetryAttempts => number,
      short number when number >= 0 && number <= MaxRetryAttempts => number,
      int number when number >= 0 && number <= MaxRetryAttempts => number,
      long number when number >= 0 && number <= MaxRetryAttempts => (int)number,
      _ => MaxRetryAttempts
    };
  }

  private static void ValidateEventId(
      ReadOnlyMemory<byte> body,
      Guid messageId)
  {
    try
    {
      using var document = JsonDocument.Parse(body);

      if (document.RootElement.ValueKind != JsonValueKind.Object)
      {
        throw new PermanentMessageException(
            "Payload deve conter um objeto JSON.");
      }

      var eventIdProperty = document.RootElement
          .EnumerateObject()
          .FirstOrDefault(property =>
              string.Equals(
                  property.Name,
                  "EventId",
                  StringComparison.OrdinalIgnoreCase));

      if (eventIdProperty.Value.ValueKind != JsonValueKind.String ||
          !Guid.TryParse(
              eventIdProperty.Value.GetString(),
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

  private async Task CloseConnectionAsync()
  {
    if (_consumerChannel is not null)
    {
      try
      {
        if (_consumerChannel.IsOpen)
          await _consumerChannel.CloseAsync();

        _consumerChannel.Dispose();
      }
      catch (Exception exception)
      {
        _logger.LogDebug(
            exception,
            "Erro ao fechar canal de consumo RabbitMQ.");
      }

      _consumerChannel = null;
    }

    if (_publisherChannel is not null)
    {
      try
      {
        if (_publisherChannel.IsOpen)
          await _publisherChannel.CloseAsync();

        _publisherChannel.Dispose();
      }
      catch (Exception exception)
      {
        _logger.LogDebug(
            exception,
            "Erro ao fechar canal de publicação RabbitMQ.");
      }

      _publisherChannel = null;
    }

    if (_connection is not null)
    {
      try
      {
        if (_connection.IsOpen)
          await _connection.CloseAsync();

        _connection.Dispose();
      }
      catch (Exception exception)
      {
        _logger.LogDebug(
            exception,
            "Erro ao fechar conexão RabbitMQ.");
      }

      _connection = null;
    }
  }
}
