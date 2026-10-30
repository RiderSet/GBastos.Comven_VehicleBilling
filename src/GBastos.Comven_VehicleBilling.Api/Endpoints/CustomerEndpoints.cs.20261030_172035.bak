using Comven_VehicleBilling.Api.Contracts.Customers;
using Comven_VehicleBilling.Application.Customers;

namespace Comven_VehicleBilling.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers");

        group.MapPost("/", async (
            CreateCustomerRequest request,
            CustomerService service,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateCustomerCommand(
                request.Name,
                request.Document,
                request.Email);

            var customer = await service.CreateAsync(
                command,
                cancellationToken);

            var response = new CustomerResponse(
                customer.Id,
                customer.Name,
                customer.Document,
                customer.Email);

            return Results.Created(
                $"/api/customers/{response.Id}",
                response);
        })
        .WithName("CreateCustomer")
        .WithSummary("Cadastra um cliente")
        .Produces<CustomerResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}