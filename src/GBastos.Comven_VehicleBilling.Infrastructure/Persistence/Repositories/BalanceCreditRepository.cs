using System.Data;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.BalanceCredits;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

public sealed class BalanceCreditRepository : IBalanceCreditRepository
{
    private readonly VehicleBillingDbContext _dbContext;

    public BalanceCreditRepository(VehicleBillingDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
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
                "A referência externa é obrigatória.",
                nameof(externalReference));

        externalReference = externalReference.Trim();

        if (externalReference.Length > 200)
            throw new ArgumentException(
                "A referência externa aceita até 200 caracteres.",
                nameof(externalReference));

        var money = Money.Positive(amount);

        try
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            // Serializa alterações de saldo do mesmo cliente.
            var customer = await _dbContext.Customers
                .FromSqlInterpolated(
                    $"SELECT * FROM customers WHERE id = {customerId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (customer is null)
                throw new KeyNotFoundException(
                    "Cliente não encontrado.");

            // A referência externa é única globalmente.
            var existing = await _dbContext.BalanceCredits
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    credit => credit.ExternalReference == externalReference,
                    cancellationToken);

            if (existing is not null)
            {
                EnsureSameCredit(existing, customerId, money.Amount);

                await transaction.CommitAsync(cancellationToken);

                return new BalanceCreditResult(
                    existing.Id,
                    customerId,
                    existing.Amount.Amount,
                    customer.Balance.Amount,
                    existing.CreatedAt,
                    true);
            }

            var now = DateTimeOffset.UtcNow;

            customer.Credit(money);

            var credit = new BalanceCredit(
                Guid.NewGuid(),
                customerId,
                externalReference,
                money,
                now);

            await _dbContext.BalanceCredits.AddAsync(
                credit,
                cancellationToken);

            // Todo crédito gera exatamente um lançamento no Ledger.
            var ledgerEntry = LedgerEntry.CreateCredit(
                Guid.NewGuid(),
                customerId,
                credit.Id,
                money,
                $"Crédito de saldo - referência {externalReference}",
                now);

            await _dbContext.LedgerEntries.AddAsync(
                ledgerEntry,
                cancellationToken);

            // Saldo, crédito e Ledger na mesma transação.
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
        catch (DbUpdateException exception)
            when (IsExternalReferenceUniqueViolation(exception))
        {
            // A transação anterior foi descartada.
            _dbContext.ChangeTracker.Clear();

            // Recupera o crédito confirmado pela outra requisição.
            var existing = await _dbContext.BalanceCredits
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    credit => credit.ExternalReference == externalReference,
                    cancellationToken);

            if (existing is null)
                throw;

            EnsureSameCredit(existing, customerId, money.Amount);

            // Recupera o saldo atualizado do cliente correto.
            var customer = await _dbContext.Customers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == customerId,
                    cancellationToken);

            if (customer is null)
                throw new KeyNotFoundException(
                    "Cliente não encontrado.");

            return new BalanceCreditResult(
                existing.Id,
                customerId,
                existing.Amount.Amount,
                customer.Balance.Amount,
                existing.CreatedAt,
                true);
        }
    }

    private static void EnsureSameCredit(
        BalanceCredit existing,
        Guid customerId,
        decimal amount)
    {
        if (existing.CustomerId != customerId ||
            existing.Amount.Amount != amount)
        {
            throw new InvalidOperationException(
                "A referência externa já foi utilizada com dados diferentes.");
        }
    }

    private static bool IsExternalReferenceUniqueViolation(
        DbUpdateException exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                string.Equals(
                    postgresException.ConstraintName,
                    "ux_balance_credits_external_reference",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
