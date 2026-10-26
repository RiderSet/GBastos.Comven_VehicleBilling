namespace Comven_VehicleBilling.Domain.Enums;

public enum OperationStatus
{
  Pending = 0,
  Charged = 1,
  AwaitingInvoice = 2,
  Invoiced = 3,
  Refunded = 4,
  Failed = 5
}

