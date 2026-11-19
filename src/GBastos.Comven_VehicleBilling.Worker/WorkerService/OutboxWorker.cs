
using Comven_VehicleBilling.Infrastructure.Mensageria;
using Comven_VehicleBilling.Infrastructure.Outbox;

namespace Comven_VehicleBilling.Worker.Workers;

public sealed class OutboxWorker : BackgroundService
{
    private const int BatchSize = 1;

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
                    break;
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
                    $"A mensagem {messageId} nÃ£o possui reserva.");

            try
            {
                await _publisher.PublishAsync(
                    message,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // A reserva serÃ¡ recuperada apÃ³s expirar.
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
                            "A falha da mensagem {MessageId} nÃ£o foi registrada: " +
                            "a reserva expirou ou foi substituÃ­da.",
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
                        "NÃ£o foi possÃ­vel registrar a falha da mensagem {MessageId}.",
                        messageId);
                }

                // ESSENCIAL:
                // NÃ£o marcar como processada uma mensagem cuja publicaÃ§Ã£o falhou.
                continue;
            }

            // SÃ³ chegamos aqui quando PublishAsync terminou com sucesso,
            // incluindo a confirmaÃ§Ã£o de publicaÃ§Ã£o do RabbitMQ.
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
                        "A publicaÃ§Ã£o da mensagem {MessageId} foi concluÃ­da, " +
                        "mas a reserva nÃ£o era mais vÃ¡lida. A mensagem poderÃ¡ " +
                        "ser publicada novamente.",
                        messageId);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // A publicaÃ§Ã£o pode ter sido confirmada.
                // NÃ£o presumir que ela deixou de ser entregue.
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "A publicaÃ§Ã£o da mensagem {MessageId} foi concluÃ­da, " +
                    "mas nÃ£o foi possÃ­vel registrar a conclusÃ£o na Outbox. " +
                    "A mensagem poderÃ¡ ser publicada novamente.",
                    messageId);
            }
        }
    }
}

