
namespace Comven_VehicleBilling.Application.Statements;

public sealed record StatementPageDto(
    Guid CustomerId,
    int Page,
    int PageSize,
    int TotalItems,
    IReadOnlyList<StatementEntryDto> Items
);
