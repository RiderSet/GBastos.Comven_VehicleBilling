using Comven_VehicleBilling.Api.Contracts.Invoices;
using Comven_VehicleBilling.Application.Invoices;

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
            .WithSummary("Gera uma fatura para um cliente pós-pago")
            .Produces<InvoiceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetInvoiceById)
            .WithName("GetInvoiceById")
            .WithSummary("Consulta uma fatura pelo identificador")
            .Produces<InvoiceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", GetInvoices)
            .WithName("GetInvoices")
            .WithSummary("Lista as faturas, opcionalmente filtradas por cliente")
            .Produces<IReadOnlyList<InvoiceResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateInvoice(
        CreateInvoiceRequest request,
        InvoiceService service,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
            return Results.Problem(
                title: "Cliente inválido",
                detail: "Informe um identificador válido.",
                statusCode: StatusCodes.Status400BadRequest);

        if (request.ReferenceDate == DateOnly.MinValue)
            return Results.Problem(
                title: "Data de referência inválida",
                detail: "Informe uma data de referência válida.",
                statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var invoice = await service.CreateAsync(
                new CreateInvoiceCommand(
                    request.CustomerId,
                    request.ReferenceDate),
                cancellationToken);

            var response = ToResponse(invoice);

            return Results.Created(
                $"/api/invoices/{invoice.Id}",
                response);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Dados da fatura inválidos",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Não foi possível gerar a fatura",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetInvoiceById(
        Guid id,
        InvoiceService service,
        CancellationToken cancellationToken)
    {
        var invoice = await service.GetByIdAsync(
            id, cancellationToken);

        return invoice is null
            ? Results.NotFound(new
            {
                message = "Fatura não encontrada.",
                invoiceId = id
            })
            : Results.Ok(ToResponse(invoice));
    }

    private static async Task<IResult> GetInvoices(
        Guid? customerId,
        InvoiceService service,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
            return Results.Problem(
                title: "Cliente inválido",
                detail: "O identificador do cliente não pode ser vazio.",
                statusCode: StatusCodes.Status400BadRequest);

        var invoices = await service.GetAllAsync(
            customerId, cancellationToken);

        return Results.Ok(invoices.Select(ToResponse).ToList());
    }

    private static InvoiceResponse ToResponse(InvoiceDto invoice)
        => new(
            invoice.Id,
            invoice.CustomerId,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.Amount,
            invoice.Status,
            invoice.CreatedAt);
}
