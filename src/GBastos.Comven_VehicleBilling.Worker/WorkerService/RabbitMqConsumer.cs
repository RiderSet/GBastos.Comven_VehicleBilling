using Comven_VehicleBilling.Infrastructure.Inbox;
using Comven_VehicleBilling.WorkerService.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Comven_VehicleBilling.WorkerService;

public sealed class RabbitMqConsumer : BackgroundService
{

  private readonly RabbitMqConsumerOptions _options;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<RabbitMqConsumer> _logger;

  private IConnection? _connection;
  private IChannel? _consumerChannel;
  private IChannel? _publisherChannel;

  public RabbitMqConsumer(
      IServiceScopeFactory scopeFactory,
      ILogger<RabbitMqConsumer> logger,
      RabbitMqConsumerOptions options)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
    _options = options;
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
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Consumer {ConsumerName} aguardando mensagens na fila {QueueName}.",
            _options.ConsumerName,
            _options.QueueName);

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
              "ConexÃ£o ou canal RabbitMQ indisponÃ­vel. Nova conexÃ£o serÃ¡ estabelecida.");
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
            _options.ReconnectIntervalSeconds);
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
              TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds),
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
    var user = _options.UserName;
    var password = _options.Password;
    var host = _options.HostName;
    var virtualHost = _options.VirtualHost;
    var port = _options.Port;

    if (string.IsNullOrWhiteSpace(user) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(host) ||
        string.IsNullOrWhiteSpace(virtualHost))
    {
      throw new InvalidOperationException(
          "Configure RABBITMQ_USER, RABBITMQ_PASSWORD, " +
          "RABBITMQ_HOST e RABBITMQ_VHOST nas variÃ¡veis de ambiente.");
    }



    if (port is < 1 or > 65535)
    {
      throw new InvalidOperationException(
          "RABBITMQ_PORT deve estar entre 1 e 65535.");
    }

    var factory = new ConnectionFactory
    {
      HostName = host,
      Port = port,
      UserName = user,
      Password = password,
      VirtualHost = virtualHost,
      AutomaticRecoveryEnabled = false
    };

    _connection = await factory.CreateConnectionAsync(
        cancellationToken);

    _consumerChannel = await _connection.CreateChannelAsync(
        cancellationToken: cancellationToken);

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
        "ConexÃ£o RabbitMQ estabelecida. Fila principal: {QueueName}.",
        _options.QueueName);
  }

  private async Task DeclareTopologyAsync(
      CancellationToken cancellationToken)
  {
    var channel = _consumerChannel!;

    await channel.ExchangeDeclareAsync(
        exchange: _options.DeadLetterExchange,
        type: ExchangeType.Fanout,
        durable: true,
        autoDelete: false,
        arguments: null,
        cancellationToken: cancellationToken);

    await channel.QueueDeclareAsync(
        queue: _options.DeadLetterQueue,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null,
        cancellationToken: cancellationToken);

    await channel.QueueBindAsync(
        queue: _options.DeadLetterQueue,
        exchange: _options.DeadLetterExchange,
        routingKey: string.Empty,
        arguments: null,
        cancellationToken: cancellationToken);

    var mainQueueArguments = new Dictionary<string, object?>
    {
      ["x-dead-letter-exchange"] = _options.DeadLetterExchange
    };

    await channel.QueueDeclareAsync(
        queue: _options.QueueName,
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
    foreach (var retry in _options.RetryQueues)
    {
      var arguments = new Dictionary<string, object?>
      {
        ["x-message-ttl"] = retry.TtlMilliseconds,
        ["x-dead-letter-exchange"] = string.Empty,
        ["x-dead-letter-routing-key"] = _options.QueueName
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
          "Mensagem sem MessageId GUID vÃ¡lido. DeliveryTag={DeliveryTag}.",
          args.DeliveryTag);

      await RejectMessageAsync(channel, args.DeliveryTag);
      return;
    }

    try
    {
      ValidateEventId(args.Body, messageId);

      await using var scope = _scopeFactory.CreateAsyncScope();

      var services = scope.ServiceProvider;

      var dbContext =
          services.GetRequiredService<VehicleBillingDbContext>();

      var inbox =
          services.GetRequiredService<InboxRepository>();

      await using var transaction =
          await dbContext.Database.BeginTransactionAsync(
              cancellationToken);

      var registered = await inbox.TryRegisterAsync(
          messageId,
          _options.ConsumerName,
          cancellationToken);

      if (registered)
      {
        var messageType = args.BasicProperties.Type;

        switch (messageType)
        {
          case "OperationCreated.v1":
            await services
                .GetRequiredService<OperationCreatedHandler>()
                .HandleAsync(args.Body, cancellationToken);
            break;

          case "OperationRefunded.v1":
            await services
                .GetRequiredService<OperationRefundedHandler>()
                .HandleAsync(args.Body, cancellationToken);
            break;

          case "InvoiceCreated.v1":
            await services
                .GetRequiredService<InvoiceCreatedHandler>()
                .HandleAsync(args.Body, cancellationToken);
            break;

          case "SupplementaryInvoiceCreated.v1":
            await services
                .GetRequiredService<SupplementaryInvoiceCreatedHandler>()
                .HandleAsync(args.Body, cancellationToken);
            break;

          default:
            throw new PermanentMessageException(
                $"Tipo de mensagem nÃ£o suportado: {messageType ?? "(vazio)"}.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Mensagem {MessageId} processada. Preparando commit PostgreSQL.",
            messageId);
      }
      else
      {
        _logger.LogInformation(
            "Mensagem {MessageId} jÃ¡ consta na Inbox. Nenhum efeito serÃ¡ repetido.",
            messageId);
      }

      await transaction.CommitAsync(cancellationToken);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
      return;
    }
    catch (PermanentMessageException exception)
    {
      _logger.LogError(
          exception,
          "Mensagem {MessageId} rejeitada por conteÃºdo invÃ¡lido.",
          messageId);

      await RejectMessageAsync(channel, args.DeliveryTag);
      return;
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha no processamento transacional de {MessageId}.",
          messageId);

      await HandleTransientFailureAsync(args, messageId);
      return;
    }

    try
    {
      if (!channel.IsOpen)
      {
        _logger.LogWarning(
            "PostgreSQL confirmou {MessageId}, mas o canal RabbitMQ estÃ¡ fechado. A mensagem poderÃ¡ ser reentregue.",
            messageId);

        return;
      }

      await channel.BasicAckAsync(
          args.DeliveryTag,
          multiple: false,
          cancellationToken: CancellationToken.None);

      _logger.LogInformation(
          "Mensagem {MessageId} confirmada no RabbitMQ apÃ³s commit PostgreSQL.",
          messageId);
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha ao confirmar mensagem {MessageId} apÃ³s COMMIT PostgreSQL. O canal serÃ¡ encerrado para permitir a reentrega.",
          messageId);

      await CloseConsumerChannelForRedeliveryAsync();
    }
  }

  private async Task HandleTransientFailureAsync(
      BasicDeliverEventArgs args,
      Guid messageId)
  {
    var consumerChannel = _consumerChannel;
    var publisherChannel = _publisherChannel;

    // 1. Verifica o canal consumidor.
    if (consumerChannel is null || !consumerChannel.IsOpen)
    {
      _logger.LogWarning(
          "Canal consumidor indisponÃ­vel para processar retry de {MessageId}. A mensagem serÃ¡ recuperada pelo RabbitMQ.",
          messageId);

      return;
    }

    // 2. Se o publicador estiver indisponÃ­vel,
    // fecha o consumidor para provocar a reentrega.
    if (publisherChannel is null || !publisherChannel.IsOpen)
    {
      _logger.LogWarning(
          "Canal publicador indisponÃ­vel para retry de {MessageId}. " +
          "Fechando canal consumidor para permitir reentrega.",
          messageId);

      await CloseConsumerChannelForRedeliveryAsync();
      return;
    }

    // 3. Recupera o nÃºmero de tentativas.
    var retryCount = GetRetryCount(args);

    // 4. Esgotamento das tentativas: encaminha para DLQ.
    if (retryCount >= _options.MaxRetryAttempts)
    {
      _logger.LogError(
          "Mensagem {MessageId} esgotou as {MaxAttempts} tentativas. Encaminhando para DLQ.",
          messageId,
          _options.MaxRetryAttempts);

      await RejectMessageAsync(
          consumerChannel,
          args.DeliveryTag);

      return;
    }

    // 5. Seleciona a prÃ³xima fila de retry.
    if (retryCount >= _options.RetryQueues.Count)
    {
      _logger.LogError(
          "Mensagem {MessageId} não possui fila de retry configurada para a tentativa {RetryCount}. Encaminhando para DLQ.",
          messageId,
          retryCount);

      await RejectMessageAsync(
          consumerChannel,
          args.DeliveryTag);

      return;
    }

    var retry = _options.RetryQueues[retryCount];

    try
    {
      // 6. Preserva os headers originais.
      var headers = args.BasicProperties.Headers is { } originalHeaders
          ? new Dictionary<string, object?>(originalHeaders)
          : new Dictionary<string, object?>();

      headers[_options.RetryHeader] = retryCount + 1;

      // 7. Preserva as propriedades da mensagem.
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

      // 8. Publica primeiro. Publisher Confirms estÃ¡ habilitado.
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
          "Falha ao publicar retry de {MessageId}. O canal serÃ¡ fechado para permitir reentrega.",
          messageId);

      await CloseConsumerChannelForRedeliveryAsync();
      return;
    }

    // 9. Somente apÃ³s publicaÃ§Ã£o confirmada executa ACK original.
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
          _options.MaxRetryAttempts,
          retry.TtlMilliseconds / 1000);
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Retry publicado para {MessageId}, mas o ACK da mensagem original falhou. Fechando canal para permitir reentrega. A Inbox protegerÃ¡ contra efeitos duplicados.",
          messageId);

      await CloseConsumerChannelForRedeliveryAsync();
    }
  }

  private async Task CloseConsumerChannelForRedeliveryAsync()
  {
    var channel = _consumerChannel;

    if (channel is null)
      return;

    try
    {
      if (channel.IsOpen)
        await channel.CloseAsync();

      _logger.LogWarning(
          "Canal de consumo fechado. Mensagens sem ACK poderÃ£o ser reentregues pelo RabbitMQ.");
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha ao fechar canal de consumo para reentrega.");
    }
  }

  private int GetRetryCount(BasicDeliverEventArgs args)
  {
    var headers = args.BasicProperties.Headers;

    if (headers is null ||
        !headers.TryGetValue(_options.RetryHeader, out var value))
    {
      return 0;
    }

    var retryCount = value switch
    {
      byte number => number,

      short number when number >= 0 => number,

      int number when number >= 0 => number,

      long number when number >= 0 && number <= int.MaxValue
          => (int)number,

      string text when int.TryParse(text, out var number) && number >= 0
          => number,

      byte[] bytes when int.TryParse(
          System.Text.Encoding.UTF8.GetString(bytes),
          out var number) && number >= 0
          => number,

      _ => _options.MaxRetryAttempts
    };

    return Math.Min(
        retryCount,
        _options.MaxRetryAttempts);
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
            "EventId do payload nÃ£o Ã© um GUID vÃ¡lido.");
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
          "Payload JSON invÃ¡lido.",
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

  private async Task RejectMessageAsync(
      IChannel channel,
      ulong deliveryTag)
  {
    try
    {
      if (!channel.IsOpen)
        return;

      await channel.BasicNackAsync(
          deliveryTag,
          multiple: false,
          requeue: false,
          cancellationToken: CancellationToken.None);

      _logger.LogWarning(
          "Mensagem rejeitada e encaminhada para Dead Letter Exchange. DeliveryTag={DeliveryTag}.",
          deliveryTag);
    }
    catch (Exception exception)
    {
      _logger.LogError(
          exception,
          "Falha ao rejeitar mensagem {DeliveryTag}. O canal serÃ¡ encerrado para permitir reentrega.",
          deliveryTag);

      await CloseConsumerChannelForRedeliveryAsync();
    }
  }

  private async Task CloseConnectionAsync()
  {
    if (_consumerChannel is not null)
    {
      var channel = _consumerChannel;
      _consumerChannel = null;

      try
      {
        if (channel.IsOpen)
          await channel.CloseAsync();
      }
      catch (Exception exception)
      {
        _logger.LogDebug(exception, "Erro ao fechar canal de consumo RabbitMQ.");
      }
      finally
      {
        channel.Dispose();
      }
    }

    if (_publisherChannel is not null)
    {
      var channel = _publisherChannel;
      _publisherChannel = null;

      try
      {
        if (channel.IsOpen)
          await channel.CloseAsync();
      }
      catch (Exception exception)
      {
        _logger.LogDebug(exception, "Erro ao fechar canal de publicaÃ§Ã£o RabbitMQ.");
      }
      finally
      {
        channel.Dispose();
      }
    }

    if (_connection is not null)
    {
      var connection = _connection;
      _connection = null;

      try
      {
        if (connection.IsOpen)
          await connection.CloseAsync();
      }
      catch (Exception exception)
      {
        _logger.LogDebug(exception, "Erro ao fechar conexÃ£o RabbitMQ.");
      }
      finally
      {
        connection.Dispose();
      }
    }
  }

}






