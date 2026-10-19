using Comven_VehicleBilling.Api.Contracts.Invoices;

namespace Comven_VehicleBilling.Api.Endpoints;

public static class InvoiceEndpoints
{
  public static IEndpointRouteBuilder MapInvoiceEndpoints(
      this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/invoices")
        .WithTags("Invoices");

    group.MapPost("/", CreateInvoice)
        .WithName("CreateInvoice")
        .WithSummary("Solicita a criação de uma fatura")
        .Produces<InvoiceResponse>(
            StatusCodes.Status201Created)
        .ProducesProblem(
            StatusCodes.Status501NotImplemented);

    group.MapGet("/{id:guid}", GetInvoiceById)
        .WithName("GetInvoiceById")
        .WithSummary("Consulta uma fatura pelo identificador")
        .Produces<InvoiceResponse>(
            StatusCodes.Status200OK)
        .ProducesProblem(
            StatusCodes.Status501NotImplemented);

    group.MapGet("/", GetInvoices)
        .WithName("GetInvoices")
        .WithSummary("Lista as faturas")
        .ProducesProblem(
            StatusCodes.Status501NotImplemented);

    return app;
  }

  private static IResult CreateInvoice(
      CreateInvoiceRequest request)
  {
    // TODO: chamar o caso de uso da Application.
    return Results.Problem(
        title: "Caso de uso não implementado",
        detail: "Conecte este endpoint ao serviço de criação de faturas.",
        statusCode: StatusCodes.Status501NotImplemented);
  }

  private static IResult GetInvoiceById(Guid id)
  {
    // TODO: consultar a Application.
    return Results.Problem(
        title: "Caso de uso não implementado",
        detail: "Conecte este endpoint ao serviço de consulta de faturas.",
        statusCode: StatusCodes.Status501NotImplemented);
  }

  private static IResult GetInvoices()
  {
    // TODO: implementar listagem e paginação na Application.
    return Results.Problem(
        title: "Caso de uso não implementado",
        detail: "Conecte este endpoint ao serviço de listagem de faturas.",
        statusCode: StatusCodes.Status501NotImplemented);
  }
}
