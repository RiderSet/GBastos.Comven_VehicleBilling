using Comven_VehicleBilling.Api.Contracts.Operations;

namespace Comven_VehicleBilling.Api.Endpoints;

public static class OperationEndpoints
{
  public static IEndpointRouteBuilder MapOperationEndpoints(
      this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/operations")
        .WithTags("Operations");

    group.MapPost("/", CreateOperation)
        .WithName("CreateOperation")
        .WithSummary("Registra uma operação")
        .Produces<OperationResponse>(
            StatusCodes.Status201Created)
        .ProducesProblem(
            StatusCodes.Status501NotImplemented);

    group.MapGet("/{id:guid}", GetOperationById)
        .WithName("GetOperationById")
        .WithSummary("Consulta uma operação pelo identificador")
        .Produces<OperationResponse>(
            StatusCodes.Status200OK)
        .ProducesProblem(
            StatusCodes.Status501NotImplemented);

    group.MapGet("/", GetOperations)
        .WithName("GetOperations")
        .WithSummary("Lista as operações")
        .ProducesProblem(
            StatusCodes.Status501NotImplemented);

    return app;
  }

  private static IResult CreateOperation(
      CreateOperationRequest request)
  {
    // TODO: chamar o caso de uso da Application.
    return Results.Problem(
        title: "Caso de uso não implementado",
        detail: "Conecte este endpoint ao serviço de registro de operações.",
        statusCode: StatusCodes.Status501NotImplemented);
  }

  private static IResult GetOperationById(Guid id)
  {
    // TODO: consultar a Application.
    return Results.Problem(
        title: "Caso de uso não implementado",
        detail: "Conecte este endpoint ao serviço de consulta de operações.",
        statusCode: StatusCodes.Status501NotImplemented);
  }

  private static IResult GetOperations()
  {
    // TODO: implementar listagem e paginação na Application.
    return Results.Problem(
        title: "Caso de uso não implementado",
        detail: "Conecte este endpoint ao serviço de listagem de operações.",
        statusCode: StatusCodes.Status501NotImplemented);
  }
}
