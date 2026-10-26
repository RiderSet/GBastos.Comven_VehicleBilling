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

      var lockId = message.LockId
          ?? throw new InvalidOperationException(
              $"A mensagem {message.Id} não possui reserva.");

      try
      {
        await _publisher.PublishAsync(
            message,
            cancellationToken);
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        // Não continuar processando o lote após o cancelamento.
        // A reserva poderá ser recuperada após sua expiração.
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
          message.RegisterFailure(
              lockId,
              exception.Message,
              DateTime.UtcNow.AddSeconds(delaySeconds));

          await repository.SaveChangesAsync(
              cancellationToken);

          _logger.LogWarning(
              exception,
              "Falha ao publicar mensagem {MessageId}. " +
              "Nova tentativa prevista em {DelaySeconds} segundos.",
              message.Id,
              delaySeconds);
        }
        catch (InvalidOperationException lockException)
        {
          _logger.LogWarning(
              lockException,
              "Não foi possível registrar a falha da mensagem " +
              "{MessageId}; a reserva pode ter expirado.",
              message.Id);
        }

        continue;
      }

      try
      {
        message.MarkAsProcessed(lockId);

        await repository.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Mensagem {MessageId} publicada e registrada na Outbox.",
            message.Id);
      }
      catch (InvalidOperationException lockException)
      {
        // A publicação pode já ter sido confirmada pelo RabbitMQ.
        // Não presumir que a mensagem deixou de ser entregue.
        _logger.LogError(
            lockException,
            "A mensagem {MessageId} foi publicada, mas não foi " +
            "possível registrá-la como processada. " +
            "Ela poderá ser publicada novamente após a reserva expirar.",
            message.Id);
      }
    }
  }
}
