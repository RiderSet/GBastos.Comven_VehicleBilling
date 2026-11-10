using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Inbox;

public sealed class InboxRepository
{
    private readonly VehicleBillingDbContext _dbContext;

    public InboxRepository(VehicleBillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Registra a mensagem atomicamente. A chave primária composta
    /// (id, consumer) decide qual entrega vence em cenários concorrentes.
    /// Deve ser chamada dentro da transação que engloba o processamento.
    /// </summary>
    public async Task<bool> TryRegisterAsync(
        Guid messageId,
        string consumer,
        CancellationToken cancellationToken)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException(
                "O identificador da mensagem não pode ser vazio.",
                nameof(messageId));

        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        var affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages (id, consumer, processed_at)
            VALUES ({messageId}, {consumer}, {DateTime.UtcNow})
            ON CONFLICT (id, consumer) DO NOTHING
            """,
            cancellationToken);

        return affectedRows == 1;
    }
}
