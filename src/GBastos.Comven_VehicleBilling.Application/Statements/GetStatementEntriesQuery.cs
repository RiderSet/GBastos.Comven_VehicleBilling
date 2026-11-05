using MediatR;

namespace Comven_VehicleBilling.Application.Statements;

public sealed record GetStatementEntriesQuery(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 50
) : IRequest<StatementPageDto>;
