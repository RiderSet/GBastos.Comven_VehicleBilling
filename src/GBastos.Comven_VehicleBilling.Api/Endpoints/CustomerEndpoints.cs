using Comven_VehicleBilling.Api.Contracts.Customers;
using Comven_VehicleBilling.Application.BalanceCredits;
using Comven_VehicleBilling.Application.Customers;
using Comven_VehicleBilling.Domain.Enums;

namespace Comven_VehicleBilling.Api.Endpoints;

public static class CustomerEndpoints
{
  public static IEndpointRouteBuilder MapCustomerEndpoints(
      this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/customers")
        .WithTags("Customers");

    // GET: Listar clientes
    group.MapGet("/", async (
        CustomerService service,
        CancellationToken cancellationToken) =>
    {
      var customers = await service.GetAllAsync(
          cancellationToken);

      var response = customers
          .Select(ToResponse)
          .ToList();

      return Results.Ok(response);
    })
    .WithName("GetAllCustomers")
    .WithSummary("Lista todos os clientes")
    .Produces<IReadOnlyList<CustomerResponse>>(StatusCodes.Status200OK);

    // GET: Consultar cliente por ID
    group.MapGet("/{customerId:guid}", async (
        Guid customerId,
        CustomerService service,
        CancellationToken cancellationToken) =>
    {
      try
      {
        var customer = await service.GetByIdAsync(
            customerId,
            cancellationToken);

        return customer is null
            ? Results.NotFound(new
            {
              error = "Cliente não encontrado."
            })
            : Results.Ok(ToResponse(customer));
      }
      catch (ArgumentException exception)
      {
        return Results.BadRequest(new
        {
          error = exception.Message
        });
      }
    })
    .WithName("GetCustomerById")
    .WithSummary("Consulta um cliente pelo identificador")
    .Produces<CustomerResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound);

    // GET: Consultar saldo
    group.MapGet("/{customerId:guid}/balance",
        async Task<IResult> (
            Guid customerId,
            CustomerService service,
            CancellationToken cancellationToken) =>
        {
          try
          {
            var balance = await service.GetBalanceAsync(
                    customerId,
                    cancellationToken);

            return balance is null
                    ? Results.NotFound(new
                    {
                      error = "Cliente não encontrado."
                    })
                    : Results.Ok(balance);
          }
          catch (ArgumentException exception)
          {
            return Results.BadRequest(new
            {
              error = exception.Message
            });
          }
        })
        .WithName("GetCustomerBalance")
        .WithSummary("Consulta o saldo do cliente")
        .Produces<CustomerBalanceDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

    // POST: Cadastrar cliente
    group.MapPost("/", async Task<IResult> (
        CreateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken) =>
    {
      try
      {
        CustomerPlan? plan = request.Plan?.Trim() switch
        {
          "Prepaid" => CustomerPlan.Prepaid,
          "Postpaid" => CustomerPlan.Postpaid,
          _ => null
        };

        if (plan is null)
        {
          return Results.BadRequest(new
          {
            error = "Plano inválido. Informe Prepaid ou Postpaid."
          });
        }

        var command = new CreateCustomerCommand(
            request.Name,
            request.Document,
            request.Email,
            plan.Value);

        var customer = await service.CreateAsync(
            command,
            cancellationToken);

        var response = ToResponse(customer);

        return Results.Created(
            $"/api/customers/{response.Id}",
            response);
      }
      catch (ArgumentException exception)
      {
        return Results.BadRequest(new
        {
          error = exception.Message
        });
      }
    })
    .WithName("CreateCustomer")
    .WithSummary("Cadastra um cliente")
    .Produces<CustomerResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest);

    // POST: Adicionar crédito ao saldo
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

  // Mapeamento único da resposta pública da API
  private static CustomerResponse ToResponse(CustomerDto customer)
  {
    return new CustomerResponse(
        customer.Id,
        customer.Name,
        customer.Document,
        customer.Email,
        customer.Plan.ToString());
  }
}

public sealed record CreateBalanceCreditRequest(
    string ExternalReference,
    decimal Amount);
