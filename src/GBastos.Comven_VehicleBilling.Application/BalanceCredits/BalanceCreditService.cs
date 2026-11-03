using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.BalanceCredits;

public sealed class BalanceCreditService
{
    private readonly IBalanceCreditRepository _repository;

    public BalanceCreditService(IBalanceCreditRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<BalanceCreditResult> CreditAsync(
        Guid customerId,
        string externalReference,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException(
                "O identificador do cliente é obrigatório.");

        if (string.IsNullOrWhiteSpace(externalReference))
            throw new ArgumentException(
                "A referência externa é obrigatória.");

        externalReference = externalReference.Trim();

        if (externalReference.Length > 200)
            throw new ArgumentException(
                "A referência externa aceita até 200 caracteres.");

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "O valor deve ser maior que zero.");

        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentException(
                "O valor deve ter no máximo duas casas decimais.");

        return _repository.CreditAsync(
            customerId,
            externalReference,
            amount,
            cancellationToken);
    }
}
