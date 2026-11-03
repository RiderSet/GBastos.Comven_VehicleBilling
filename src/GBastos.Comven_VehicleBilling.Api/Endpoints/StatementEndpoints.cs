using Comven_VehicleBilling.Application.Statements;

namespace Comven_VehicleBilling.Api.Endpoints;

public static class StatementEndpoints
{
    public static IEndpointRouteBuilder MapStatementEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/statements")
            .WithTags("Statements");

        group.MapGet("/{customerId:guid}", GetStatement)
            .WithName("GetStatement")
            .WithSummary("Consulta o extrato de um cliente")
            .Produces<StatementDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetStatement(
        Guid customerId,
        DateOnly startDate,
        DateOnly endDate,
        StatementService service,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            return Results.Problem(
                title: "Cliente inválido",
                detail: "Informe um identificador válido.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (startDate > endDate)
        {
            return Results.Problem(
                title: "Período inválido",
                detail: "A data inicial não pode ser posterior à data final.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var query = new GetStatementQuery(
            customerId, startDate, endDate);

        var statement = await service.GetAsync(
            query, cancellationToken);

        return statement is null
            ? Results.NotFound(new { message = "Cliente não encontrado.", customerId })
            : Results.Ok(statement);
    }
}
