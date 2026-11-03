using System.Data;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.BalanceCredits;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

public sealed class BalanceCreditRepository : IBalanceCreditRepository
{
    private readonly VehicleBillingDbContext _dbContext;

    public BalanceCreditRepository(VehicleBillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BalanceCreditResult> CreditAsync(
        Guid customerId,
        string externalReference,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException(
                "O cliente é obrigatório.", nameof(customerId));

        if (string.IsNullOrWhiteSpace(externalReference))
            throw new ArgumentException(
                "A referência externa é obrigatória.", nameof(externalReference));

        externalReference = externalReference.Trim();

        if (externalReference.Length > 200)
            throw new ArgumentException(
                "A referência externa aceita até 200 caracteres.",
                nameof(externalReference));

        var money = Money.Positive(amount);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Serializa alterações concorrentes no saldo do mesmo cliente.
        var customer = await _dbContext.Customers
            .FromSqlInterpolated(
                $"SELECT * FROM customers WHERE id = {customerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (customer is null)
            throw new KeyNotFoundException("Cliente não encontrado.");

        // Verifica se a referência já foi processada.
        var existing = await _dbContext.BalanceCredits
            .AsNoTracking()
            .SingleOrDefaultAsync(
                credit => credit.ExternalReference == externalReference,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.CustomerId != customerId ||
                existing.Amount.Amount != amount)
            {
                throw new InvalidOperationException(
                    "A referência externa já foi utilizada com dados diferentes.");
            }

            await transaction.CommitAsync(cancellationToken);

            return new BalanceCreditResult(
                existing.Id,
                customerId,
                existing.Amount.Amount,
                customer.Balance.Amount,
                existing.CreatedAt,
                true);
        }

        // Atualiza o saldo e registra a recarga na mesma transação.
        customer.Credit(money);

        var now = DateTimeOffset.UtcNow;

        var credit = new BalanceCredit(
            Guid.NewGuid(),
            customerId,
            externalReference,
            money,
            now);

        await _dbContext.BalanceCredits.AddAsync(
            credit, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BalanceCreditResult(
            credit.Id,
            customerId,
            credit.Amount.Amount,
            customer.Balance.Amount,
            credit.CreatedAt,
            false);
    }
}