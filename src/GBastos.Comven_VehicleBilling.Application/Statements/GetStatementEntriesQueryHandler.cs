using Comven_VehicleBilling.Application.Abstractions.Persistence;
using MediatR;

namespace Comven_VehicleBilling.Application.Statements;

public sealed class GetStatementEntriesQueryHandler
    : IRequestHandler<GetStatementEntriesQuery, StatementPageDto>
{
  private readonly IStatementRepository _repository;

  public GetStatementEntriesQueryHandler(
      IStatementRepository repository)
  {
    _repository = repository;
  }

  public async Task<StatementPageDto> Handle(
      GetStatementEntriesQuery request,
      CancellationToken cancellationToken)
  {
    return await _repository.GetEntriesAsync(
        request,
        cancellationToken);
  }
}
