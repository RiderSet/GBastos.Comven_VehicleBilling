using Comven_VehicleBilling.Application.Abstractions.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Comven_VehicleBilling.Infrastructure.PaymentsProvider;

public sealed class PaymentProvider : IPaymentProvider
{
  private const string UniqueConstraint =
      "ux_payment_provider_transactions_idempotency_key";

  private readonly IDbContextFactory<VehicleBillingDbContext> _contextFactory;

  public PaymentProvider(
      IDbContextFactory<VehicleBillingDbContext> contextFactory)
  {
    _contextFactory = contextFactory
        ?? throw new ArgumentNullException(nameof(contextFactory));
  }

  public async Task<PaymentResult> ProcessPaymentAsync(
      PaymentRequest request,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    ValidateRequest(request);

    var key = NormalizeKey(request.IdempotencyKey);

    await using var db = await _contextFactory.CreateDbContextAsync(
        cancellationToken);

    var existing = await db.PaymentProviderTransactions
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.IdempotencyKey == key,
            cancellationToken);

    if (existing is not null)
      return ValidateExisting(existing, request);

    var transaction = new PaymentProviderTransaction
    {
      Id = Guid.NewGuid(),
      IdempotencyKey = key,
      InvoiceId = request.InvoiceId,
      CustomerId = request.CustomerId,
      Amount = request.Amount,
      Outcome = PaymentOutcome.Approved,
      ExternalPaymentId = $"PAY-{key}",
      ErrorMessage = null,
      CreatedAt = DateTime.UtcNow
    };

    db.PaymentProviderTransactions.Add(transaction);

    try
    {
      await db.SaveChangesAsync(cancellationToken);

      return ToPaymentResult(transaction);
    }
    catch (DbUpdateException exception)
        when (IsIdempotencyConflict(exception))
    {
      // Outra instância registrou o pagamento primeiro.
      // Recuperamos o registro confirmado pelo PostgreSQL.
      await using var recoveryDb =
          await _contextFactory.CreateDbContextAsync(
              cancellationToken);

      var persisted = await recoveryDb.PaymentProviderTransactions
          .AsNoTracking()
          .SingleOrDefaultAsync(
              x => x.IdempotencyKey == key,
              cancellationToken);

      if (persisted is null)
        throw;

      return ValidateExisting(persisted, request);
    }
  }

  public async Task<PaymentResult?> GetPaymentAsync(
      string idempotencyKey,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
    cancellationToken.ThrowIfCancellationRequested();

    var key = NormalizeKey(idempotencyKey);

    await using var db = await _contextFactory.CreateDbContextAsync(
        cancellationToken);

    var payment = await db.PaymentProviderTransactions
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.IdempotencyKey == key,
            cancellationToken);

    return payment is null
        ? null
        : ToPaymentResult(payment);
  }

  private static PaymentResult ValidateExisting(
      PaymentProviderTransaction payment,
      PaymentRequest request)
  {
    if (payment.InvoiceId != request.InvoiceId ||
        payment.CustomerId != request.CustomerId ||
        payment.Amount != request.Amount)
    {
      throw new InvalidOperationException(
          "The idempotency key was already used with different payment data.");
    }

    return ToPaymentResult(payment);
  }

  private static PaymentResult ToPaymentResult(
      PaymentProviderTransaction payment)
  {
    return new PaymentResult(
        payment.Outcome,
        payment.ExternalPaymentId,
        payment.ErrorMessage);
  }

  private static bool IsIdempotencyConflict(
      DbUpdateException exception)
  {
    return exception.InnerException is PostgresException
    {
      SqlState: PostgresErrorCodes.UniqueViolation,
      ConstraintName: UniqueConstraint
    };
  }

  private static string NormalizeKey(string key)
      => key.Trim().ToLowerInvariant();

  private static void ValidateRequest(PaymentRequest request)
  {
    if (request.InvoiceId == Guid.Empty)
    {
      throw new ArgumentException(
          "Invoice id cannot be empty.",
          nameof(request));
    }

    if (request.CustomerId == Guid.Empty)
    {
      throw new ArgumentException(
          "Customer id cannot be empty.",
          nameof(request));
    }

    if (request.Amount <= 0)
    {
      throw new ArgumentOutOfRangeException(
          nameof(request),
          "Payment amount must be greater than zero.");
    }

    var expectedKey = request.InvoiceId.ToString("N");

    if (!string.Equals(
            request.IdempotencyKey,
            expectedKey,
            StringComparison.OrdinalIgnoreCase))
    {
      throw new ArgumentException(
          "Idempotency key must match the invoice identifier.",
          nameof(request));
    }
  }
}
