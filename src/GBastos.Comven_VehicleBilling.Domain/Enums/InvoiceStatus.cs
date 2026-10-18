namespace Comven_VehicleBilling.Domain.Enums;

public enum InvoiceStatus
{
  Open = 1,
  PaymentPending = 2,
  Paid = 3,
  PaymentUnknown = 4,
  PaymentFailed = 5
}
