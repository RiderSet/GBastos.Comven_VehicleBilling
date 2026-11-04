using Comven_VehicleBilling.Api.Contracts.Customers;
using Comven_VehicleBilling.Application.BalanceCredits;
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

        group.MapPost("/{customerId:guid}/balance/credits",
            async Task<IResult> (
                Guid customerId,
                CreateBalanceCreditRequest request,
                BalanceCreditService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await service.CreditAsync(
                        customerId,
                        request.ExternalReference,
                        request.Amount,
                        cancellationToken);

                    if (result.AlreadyProcessed)
                        return Results.Ok(result);

                    return Results.Created(
                        $"/api/customers/{customerId}/balance/credits/{result.CreditId}",
                        result);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new
                    {
                        error = exception.Message
                    });
                }
                catch (KeyNotFoundException exception)
                {
                    return Results.NotFound(new
                    {
                        error = exception.Message
                    });
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message
                    });
                }
            })
            .WithName("CreateBalanceCredit")
            .WithSummary("Adiciona saldo pré-pago ao cliente")
            .Produces<BalanceCreditResult>(StatusCodes.Status201Created)
            .Produces<BalanceCreditResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

public sealed record CreateBalanceCreditRequest(
    string ExternalReference,
    decimal Amount);
