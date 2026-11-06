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
        .WithSummary("Consulta o extrato financeiro de um cliente")
        .Produces<StatementDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapGet("/{customerId:guid}/entries", GetStatementEntries)
        .WithName("GetStatementEntries")
        .WithSummary("Consulta os lançamentos do razão com paginação")
        .Produces<StatementPageDto>(StatusCodes.Status200OK)
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
        customerId,
        startDate,
        endDate);

    var statement = await service.GetAsync(
        query,
        cancellationToken);

    return statement is null
        ? Results.NotFound(new
        {
          message = "Cliente não encontrado.",
          customerId
        })
        : Results.Ok(statement);
  }

  private static async Task<IResult> GetStatementEntries(
      Guid customerId,
      StatementService service,
      CancellationToken cancellationToken,
      int page = 1,
      int pageSize = 50)
  {
    if (customerId == Guid.Empty)
    {
      return Results.Problem(
          title: "Cliente inválido",
          detail: "Informe um identificador válido.",
          statusCode: StatusCodes.Status400BadRequest);
    }

    if (page < 1 || pageSize < 1 || pageSize > 100)
    {
      return Results.Problem(
          title: "Paginação inválida",
          detail: "A página deve ser maior que zero e o tamanho deve estar entre 1 e 100.",
          statusCode: StatusCodes.Status400BadRequest);
    }

    var query = new GetStatementEntriesQuery(
        customerId,
        page,
        pageSize);

    try
    {
      var result = await service.GetEntriesAsync(
          query,
          cancellationToken);

      return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return Results.NotFound(new
      {
        message = "Cliente não encontrado.",
        customerId
      });
    }
  }
}
