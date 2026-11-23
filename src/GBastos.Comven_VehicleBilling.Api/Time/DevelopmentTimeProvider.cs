
namespace Comven_VehicleBilling.Api.Time;

public sealed class DevelopmentTimeProvider : global::System.TimeProvider
{
  private readonly global::System.DateTimeOffset _utcNow;

  public DevelopmentTimeProvider(string utcNow)
  {
    if (!global::System.DateTimeOffset.TryParse(
            utcNow,
            global::System.Globalization.CultureInfo.InvariantCulture,
            global::System.Globalization.DateTimeStyles.AssumeUniversal,
            out _utcNow))
    {
      throw new global::System.ArgumentException(
          "Invalid VEHICLE_BILLING_TEST_NOW_UTC value.",
          nameof(utcNow));
    }
  }

  public override global::System.DateTimeOffset GetUtcNow()
  {
    return _utcNow;
  }
}
