namespace Comven_VehicleBilling.Application.Invoices;

public sealed class ExternalPaymentIdConflictException : Exception
{
  public ExternalPaymentIdConflictException(Exception innerException)
      : base(
          "O identificador externo de pagamento já está associado a outra fatura.",
          innerException)
  {
  }
}
