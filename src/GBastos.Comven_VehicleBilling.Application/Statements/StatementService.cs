using Comven_VehicleBilling.Application.Abstractions.Persistence;

namespace Comven_VehicleBilling.Application.Statements;

public sealed class StatementService
{
    private readonly IStatementRepository _repository;

    public StatementService(IStatementRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<StatementDto?> GetAsync(
        GetStatementQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.CustomerId == Guid.Empty)
            throw new ArgumentException(
                "O cliente é obrigatório.",
                nameof(query));

        if (query.StartDate > query.EndDate)
            throw new ArgumentException(
                "A data inicial não pode ser posterior à data final.",
                nameof(query));

        return _repository.GetAsync(query, cancellationToken);
    }

    public Task<StatementPageDto> GetEntriesAsync(
        GetStatementEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.CustomerId == Guid.Empty)
            throw new ArgumentException(
                "O cliente é obrigatório.",
                nameof(query));

        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(
                nameof(query.Page),
                "A página deve ser maior que zero.");

        if (query.PageSize < 1 || query.PageSize > 100)
            throw new ArgumentOutOfRangeException(
                nameof(query.PageSize),
                "O tamanho da página deve estar entre 1 e 100.");

        return _repository.GetEntriesAsync(query, cancellationToken);
    }
}
