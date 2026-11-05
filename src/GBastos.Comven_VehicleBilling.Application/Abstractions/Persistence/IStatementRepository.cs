using Comven_VehicleBilling.Application.Statements;

namespace Comven_VehicleBilling.Application.Abstractions.Persistence;

public interface IStatementRepository
{
    Task<StatementDto?> GetAsync(
        GetStatementQuery query,
        CancellationToken cancellationToken = default);

  Task<StatementPageDto> GetEntriesAsync(
    GetStatementEntriesQuery query,
    CancellationToken cancellationToken = default);

}
