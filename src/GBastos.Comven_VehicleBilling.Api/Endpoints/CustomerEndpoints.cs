using Comven_VehicleBilling.Api.Contracts.Customers;

namespace Comven_VehicleBilling.Api.Endpoints;

public static class CustomerEndpoints
{
  public static IEndpointRouteBuilder MapCustomerEndpoints(
      this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/customers")
        .WithTags("Customers");

    group.MapPost("/", (
        CreateCustomerRequest request) =>
    {
      // Exemplo didático: aqui entraria o caso de uso
      // da camada Application.

      var response = new CustomerResponse(
          Guid.NewGuid(),
          request.Name,
          request.Document,
          request.Email);

      return Results.Created(
          $"/api/customers/{response.Id}",
          response);
    })
    .WithName("CreateCustomer")
    .WithSummary("Cadastra um cliente")
    .Produces<CustomerResponse>(
        StatusCodes.Status201Created);

    return app;
  }
}
