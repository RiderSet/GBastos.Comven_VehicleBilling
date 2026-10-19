using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Domain.Rules;

public static class OperationPricing
{
  public static decimal GetPrice(OperationType type) =>
      type switch
      {
        OperationType.VehicleConsultation => 12.50m,
        OperationType.CrlveIssuance => 45.00m,
        OperationType.PlateConsultation => 8.90m,

        _ => throw new ArgumentOutOfRangeException(
              nameof(type),
              type,
              "Tipo de operação inválido.")
      };
}
