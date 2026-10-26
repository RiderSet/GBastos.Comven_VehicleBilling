using Comven_VehicleBilling.Infrastructure.Mensageria;
using Comven_VehicleBilling.Infrastructure.Outbox;

namespace Comven_VehicleBilling.Worker.Workers;

public sealed class OutboxWorker : BackgroundService
{
  private const int BatchSize = 50;

  private static readonly TimeSpan PollingInterval =
      TimeSpan.FromSeconds(15);

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly RabbitMqPublisher _publisher;
  private readonly ILogger<OutboxWorker> _logger;

  public OutboxWorker(
      IServiceScopeFactory scopeFactory,
      RabbitMqPublisher publisher,
      ILogger<OutboxWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _publisher = publisher;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(
      CancellationToken stoppingToken)
  {
    _logger.LogInformation("OutboxWorker iniciado.");

    using var timer = new PeriodicTimer(PollingInterval);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await ProcessPendingMessagesAsync(stoppingToken);
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
            "Falha ao consultar ou processar a Outbox.");
      }

      try
      {
        if (!await timer.WaitForNextTickAsync(stoppingToken))
        {
          break;
        }
      }
      catch (OperationCanceledException)
          when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
    }

    _logger.LogInformation("OutboxWorker encerrado.");
  }

  private async Task ProcessPendingMessagesAsync(
      CancellationToken cancellationToken)
  {
    using var scope = _scopeFactory.CreateScope();

    var repository = scope.ServiceProvider
        .GetRequiredService<OutboxRepository>();

    var messages = await repository.GetPendingAsync(
        BatchSize,
        cancellationToken);

    if (messages.Count == 0)
    {
      _logger.LogDebug(
          "Nenhuma mensagem pendente na Outbox.");

      return;
    }

    _logger.LogInformation(
        "Reservadas {MessageCount} mensagens pendentes.",
        messages.Count);

    foreach (var message in messages)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var messageId = message.Id;
      var lockId = message.LockId
          ?? throw new InvalidOperationException(
              $"A mensagem {messageId} não possui reserva.");

      try
      {
        await _publisher.PublishAsync(
            message,
            cancellationToken);
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        // A reserva será recuperada após expirar.
        throw;
      }
      catch (Exception exception)
      {
        var nextAttempt = message.AttemptCount + 1;

        var delaySeconds = Math.Min(
            300,
            Math.Pow(2, Math.Min(nextAttempt - 1, 8)));

        try
        {
          var registered = await repository.RegisterFailureAsync(
              messageId,
              lockId,
              exception.Message,
              DateTime.UtcNow.AddSeconds(delaySeconds),
              cancellationToken);

          if (registered)
          {
            _logger.LogWarning(
                exception,
                "Falha ao publicar a mensagem {MessageId}. " +
                "Nova tentativa prevista em {DelaySeconds} segundos.",
                messageId,
                delaySeconds);
          }
          else
          {
            _logger.LogWarning(
                exception,
                "A falha da mensagem {MessageId} não foi registrada: " +
                "a reserva expirou ou foi substituída.",
                messageId);
          }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
          throw;
        }
        catch (Exception persistenceException)
        {
          _logger.LogError(
              persistenceException,
              "Não foi possível registrar a falha da mensagem " +
              "{MessageId}. A reserva poderá expirar.",
              messageId);
        }

        continue;
      }

      try
      {
        var registered = await repository.MarkAsProcessedAsync(
            messageId,
            lockId,
            cancellationToken);

        if (registered)
        {
          _logger.LogInformation(
              "Mensagem {MessageId} publicada e registrada na Outbox.",
              messageId);
        }
        else
        {
          _logger.LogError(
              "A publicação da mensagem {MessageId} foi concluída, " +
              "mas a reserva não era mais válida. A mensagem poderá " +
              "ser publicada novamente.",
              messageId);
        }
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        // A publicação pode ter sido confirmada.
        // Não presumir que ela deixou de ser entregue.
        throw;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "A publicação da mensagem {MessageId} foi concluída, " +
            "mas não foi possível registrar a conclusão na Outbox. " +
            "A mensagem poderá ser publicada novamente.",
            messageId);
      }
    }
  }
}
