namespace Comven_VehicleBilling.Application.Operations;

public sealed record RefundOperationResult(
    OperationDetailDto Operation,
    bool AlreadyRefunded);
