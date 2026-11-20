namespace Comven_VehicleBilling.WorkerService.Events;

public interface IOperationCreatedHandler
{
    Task HandleAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken);
}