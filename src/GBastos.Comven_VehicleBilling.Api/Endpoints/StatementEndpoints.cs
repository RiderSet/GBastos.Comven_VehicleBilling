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
        .WithDescription(
            "Informe as datas inicial e final no formato yyyy-MM-dd.")
        .ProducesProblem(
            StatusCodes.Status400BadRequest)
        .ProducesProblem(
            StatusCodes.Status501NotImplemented);

    return app;
  }

  private static IResult GetStatement(
      Guid customerId,
      DateOnly startDate,
      DateOnly endDate)
  {
    if (startDate > endDate)
    {
      return Results.Problem(
          title: "Período inválido",
          detail: "A data inicial não pode ser posterior à data final.",
          statusCode: StatusCodes.Status400BadRequest);
    }

    // TODO: chamar o caso de uso da Application.
    return Results.Problem(
        title: "Caso de uso não implementado",
        detail: "Conecte este endpoint ao serviço de consulta de extratos.",
        statusCode: StatusCodes.Status501NotImplemented);
  }
}
