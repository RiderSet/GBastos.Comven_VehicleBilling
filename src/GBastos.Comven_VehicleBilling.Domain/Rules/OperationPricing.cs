using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Domain.Rules;

public static class OperationPricing
{
  public const decimal VehicleConsultationPrice = 12.50m;
  public const decimal CrlveIssuancePrice = 45.00m;
  public const decimal PlateConsultationPrice = 8.90m;

  public static decimal GetPrice(OperationType type)
  {
    return type switch
    {
      OperationType.VehicleConsultation
          => VehicleConsultationPrice,

      OperationType.CrlveIssuance
          => CrlveIssuancePrice,

      OperationType.PlateConsultation
          => PlateConsultationPrice,

      _ => throw new ArgumentOutOfRangeException(
          nameof(type),
          type,
          "Tipo de operação inválido.")
    };
  }
}
