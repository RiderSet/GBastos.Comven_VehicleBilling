using Comven_VehicleBilling.Application.Operations;
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
        .Produces<OperationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

    group.MapGet("/{id:guid}", GetOperationById)
        .WithName("GetOperationById")
        .WithSummary("Consulta uma operação")
        .Produces<OperationResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapGet("/", GetOperations)
        .WithName("GetOperations")
        .WithSummary("Lista operações")
        .Produces<IReadOnlyList<OperationResponse>>(
            StatusCodes.Status200OK);

    return app;
  }

  private static async Task<IResult> CreateOperation(
      CreateOperationRequest request,
      OperationService service,
      CancellationToken cancellationToken)
  {
    try
    {
      var command = new CreateOperationCommand(
          request.ExternalOperationId,
          request.CustomerId,
          request.OperationType,
          request.Amount);

      var operation = await service.CreateAsync(
          command,
          cancellationToken);

      var response = ToResponse(operation);

      return Results.Created(
          $"/api/operations/{response.Id}",
          response);
    }
    catch (ArgumentException ex)
    {
      return Results.Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Requisição inválida",
          detail: ex.Message);
    }
    catch (ApplicationException ex)
    {
      return Results.Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Falha de validação",
          detail: ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      return Results.Problem(
          statusCode: StatusCodes.Status409Conflict,
          title: "Não foi possível registrar a operação",
          detail: ex.Message);
    }
  }

  private static async Task<IResult> GetOperationById(
      Guid id,
      OperationService service,
      CancellationToken cancellationToken)
  {
    var operation = await service.GetByIdAsync(
        id,
        cancellationToken);

    return operation is null
        ? Results.NotFound()
        : Results.Ok(ToResponse(operation));
  }

  private static async Task<IResult> GetOperations(
      Guid? customerId,
      OperationService service,
      CancellationToken cancellationToken)
  {
    var operations = await service.GetAllAsync(
        customerId,
        cancellationToken);

    return Results.Ok(operations.Select(ToResponse).ToList());
  }

  private static OperationResponse ToResponse(OperationDto operation)
      => new(
          operation.Id,
          operation.CustomerId,
          operation.OperationType,
          operation.Amount,
          operation.CreatedAt);
}
