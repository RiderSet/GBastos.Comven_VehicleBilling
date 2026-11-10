namespace Comven_VehicleBilling.WorkerService.Events;

public sealed class PermanentMessageException : Exception
{
    public PermanentMessageException(string message) : base(message) { }
    public PermanentMessageException(string message, Exception innerException)
        : base(message, innerException) { }
}
