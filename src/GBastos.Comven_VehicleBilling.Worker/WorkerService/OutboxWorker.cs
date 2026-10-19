using Comven_VehicleBilling.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Comven_VehicleBilling.Worker.Workers;

public sealed class OutboxWorker : BackgroundService
{
  private const int BatchSize = 50;
  private static readonly TimeSpan PollingInterval =
      TimeSpan.FromSeconds(15);

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<OutboxWorker> _logger;

  public OutboxWorker(
      IServiceScopeFactory scopeFactory,
      ILogger<OutboxWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(
      CancellationToken stoppingToken)
  {
    _logger.LogInformation(
        "OutboxWorker iniciado.");

    using var timer = new PeriodicTimer(PollingInterval);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await InspectPendingMessagesAsync(stoppingToken);
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
            "Falha ao consultar a Outbox.");
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

    _logger.LogInformation(
        "OutboxWorker encerrado.");
  }

  private async Task InspectPendingMessagesAsync(
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
        "Foram encontradas {MessageCount} mensagens pendentes.",
        messages.Count);

    foreach (var message in messages)
    {
      _logger.LogWarning(
          "Mensagem {MessageId}, tipo {MessageType}, ainda sem processador de negócio registrado.",
          message.Id,
          message.MessageType);
    }

    // Por segurança, nenhuma mensagem é alterada nesta etapa.
    // O processamento real será conectado aos eventos de negócio.
  }
}
