using Comven_VehicleBilling.Application.Abstractions.Payments;
using Comven_VehicleBilling.Infrastructure.PaymentsProvider;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.UnitTests.PaymentsProvider;

public sealed class PaymentProviderTests
{
  private static PaymentProvider CreateProvider()
      => new(new UnusedDbContextFactory());

  [Fact]
  public async Task ProcessPaymentAsync_ShouldRejectEmptyInvoiceId()
  {
    var provider = CreateProvider();
    var request = CreateRequest() with
    {
      InvoiceId = Guid.Empty
    };

    await Assert.ThrowsAsync<ArgumentException>(
        () => provider.ProcessPaymentAsync(request));
  }

  [Fact]
  public async Task ProcessPaymentAsync_ShouldRejectEmptyCustomerId()
  {
    var provider = CreateProvider();
    var request = CreateRequest() with
    {
      CustomerId = Guid.Empty
    };

    await Assert.ThrowsAsync<ArgumentException>(
        () => provider.ProcessPaymentAsync(request));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-10)]
  public async Task ProcessPaymentAsync_ShouldRejectInvalidAmount(
      decimal amount)
  {
    var provider = CreateProvider();
    var request = CreateRequest() with
    {
      Amount = amount
    };

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
        () => provider.ProcessPaymentAsync(request));
  }

  private static PaymentRequest CreateRequest()
  {
    var invoiceId = Guid.NewGuid();

    return new PaymentRequest(
        invoiceId,
        Guid.NewGuid(),
        125.50m,
        invoiceId.ToString("N"));
  }

  private sealed class UnusedDbContextFactory
      : IDbContextFactory<VehicleBillingDbContext>
  {
    public VehicleBillingDbContext CreateDbContext()
    {
      throw new InvalidOperationException(
          "A factory não deveria ser acessada neste teste.");
    }

    public Task<VehicleBillingDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
      throw new InvalidOperationException(
          "A factory não deveria ser acessada neste teste.");
    }
  }
}
