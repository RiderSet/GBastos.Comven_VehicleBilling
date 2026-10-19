using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Domain.Entities;

public sealed class ExternalCharge
{
  private ExternalCharge()
  {
    Provider = string.Empty;
  }

  public ExternalCharge(
      Guid id,
      Guid invoiceId,
      decimal amount,
      string provider)
  {
    if (id == Guid.Empty)
      throw new ArgumentException(
          "O identificador da cobrança é obrigatório.", nameof(id));

    if (invoiceId == Guid.Empty)
      throw new ArgumentException(
          "O identificador da fatura é obrigatório.", nameof(invoiceId));

    if (amount <= 0m || decimal.Round(amount, 2) != amount)
      throw new ArgumentOutOfRangeException(nameof(amount));

    if (string.IsNullOrWhiteSpace(provider))
      throw new ArgumentException(
          "O provedor é obrigatório.", nameof(provider));

    Id = id;
    InvoiceId = invoiceId;
    Amount = amount;
    Provider = provider.Trim();
    Status = ExternalChargeStatus.Pending;
    CreatedAtUtc = DateTimeOffset.UtcNow;
  }

  public Guid Id { get; private set; }
  public Guid InvoiceId { get; private set; }
  public decimal Amount { get; private set; }
  public string Provider { get; private set; }
  public ExternalChargeStatus Status { get; private set; }
  public string? ProviderReference { get; private set; }
  public string? LastError { get; private set; }
  public int AttemptCount { get; private set; }
  public DateTimeOffset CreatedAtUtc { get; private set; }
  public DateTimeOffset? LastAttemptAtUtc { get; private set; }
  public DateTimeOffset? CompletedAtUtc { get; private set; }

  public void StartAttempt()
  {
    if (Status != ExternalChargeStatus.Pending)
      throw new InvalidOperationException(
          "Somente cobranças pendentes podem iniciar uma tentativa.");

    Status = ExternalChargeStatus.Processing;
    AttemptCount++;
    LastAttemptAtUtc = DateTimeOffset.UtcNow;
    LastError = null;
  }

  public void MarkSucceeded(string? providerReference)
  {
    if (Status != ExternalChargeStatus.Processing)
      throw new InvalidOperationException(
          "A cobrança não está em processamento.");

    Status = ExternalChargeStatus.Succeeded;
    ProviderReference = providerReference;
    CompletedAtUtc = DateTimeOffset.UtcNow;
  }

  public void MarkFailed(string error)
  {
    if (Status != ExternalChargeStatus.Processing)
      throw new InvalidOperationException(
          "A cobrança não está em processamento.");

    if (string.IsNullOrWhiteSpace(error))
      throw new ArgumentException(
          "O motivo da falha é obrigatório.", nameof(error));

    Status = ExternalChargeStatus.Failed;
    LastError = error.Trim();
    CompletedAtUtc = DateTimeOffset.UtcNow;
  }

  public void MarkUnknown(string reason)
  {
    if (Status != ExternalChargeStatus.Processing)
      throw new InvalidOperationException(
          "A cobrança não está em processamento.");

    if (string.IsNullOrWhiteSpace(reason))
      throw new ArgumentException(
          "O motivo é obrigatório.", nameof(reason));

    Status = ExternalChargeStatus.Unknown;
    LastError = reason.Trim();
  }
}
