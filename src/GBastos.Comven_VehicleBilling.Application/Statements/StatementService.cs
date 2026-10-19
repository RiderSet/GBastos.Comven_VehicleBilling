using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Statements;

public sealed class StatementService
{
  private readonly IStatementRepository _repository;

  public StatementService(IStatementRepository repository)
  {
    _repository = repository;
  }

  public Task<StatementDto?> GetAsync(
      GetStatementQuery query,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (query.CustomerId == Guid.Empty)
      throw new ApplicationException("O cliente é obrigatório.");

    if (query.StartDate > query.EndDate)
      throw new ApplicationException(
          "A data inicial não pode ser posterior à data final.");

    return _repository.GetAsync(query, cancellationToken);
  }
}
