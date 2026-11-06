using Comven_VehicleBilling.Api.Contracts.Operations;
using Comven_VehicleBilling.Application.Operations;

namespace Comven_VehicleBilling.Api.Endpoints;

public static class OperationEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;

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
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetOperationById)
            .WithName("GetOperationById")
            .WithSummary("Consulta uma operação pelo identificador")
            .WithDescription(
                "Retorna os dados completos da operação, incluindo seus lançamentos financeiros.")
            .Produces<OperationDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", GetOperations)
            .WithName("GetOperations")
            .WithSummary("Lista operações com filtros e paginação")
            .Produces<PagedOperationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
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
            var customerNotFound = ex.Message.Contains(
                "cliente informado não foi encontrado",
                StringComparison.OrdinalIgnoreCase);

            return Results.Problem(
                statusCode: customerNotFound
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status409Conflict,
                title: customerNotFound
                    ? "Cliente não encontrado"
                    : "Não foi possível registrar a operação",
                detail: ex.Message);
        }
    }

    private static async Task<IResult> GetOperationById(
        Guid id,
        OperationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var operation = await service.GetByIdAsync(
                id,
                cancellationToken);

            return operation is null
                ? Results.NotFound()
                : Results.Ok(ToDetailResponse(operation));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Identificador inválido",
                detail: ex.Message);
        }
    }

    private static async Task<IResult> GetOperations(
        Guid? customerId,
        string? operationType,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        string? status,
        OperationService service,
        CancellationToken cancellationToken,
        int page = DefaultPage,
        int pageSize = DefaultPageSize)
    {
        try
        {
            var result = await service.GetAllAsync(
                customerId,
                operationType,
                startDate,
                endDate,
                page,
                pageSize,
                status,
                cancellationToken);

            var response = new PagedOperationResponse(
                result.Items
                    .Select(ToResponse)
                    .ToArray(),
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages,
                result.HasNextPage,
                result.HasPreviousPage);

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Filtro ou paginação inválida",
                detail: ex.Message);
        }
    }

    private static OperationResponse ToResponse(
        OperationDto operation)
    {
        return new OperationResponse(
            operation.Id,
            operation.ExternalOperationId,
            operation.CustomerId,
            operation.OperationType,
            operation.Amount,
            operation.Status,
            operation.CreatedAt,
            operation.RefundedAt,
            operation.InvoicedAt,
            operation.InvoiceId);
    }

    private static OperationDetailResponse ToDetailResponse(
        OperationDetailDto operation)
    {
        return new OperationDetailResponse(
            operation.Id,
            operation.CustomerId,
            operation.OperationType,
            operation.Amount,
            operation.Status,
            operation.CreatedAt,
            operation.InvoicedAt,
            operation.RefundedAt,
            operation.InvoiceId,
            operation.LedgerEntries
                .Select(entry => new OperationLedgerEntryResponse(
                    entry.Id,
                    entry.Type,
                    entry.Amount,
                    entry.Description,
                    entry.CreatedAt))
                .ToList());
    }
}
