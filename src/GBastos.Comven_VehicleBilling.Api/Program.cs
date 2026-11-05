using Comven_VehicleBilling.Api.Endpoints;
using Comven_VehicleBilling.Application;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

const string CorsPolicy = "VehicleBillingCors";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
  errorApp.Run(async context =>
  {
    var exception = context.Features
        .Get<IExceptionHandlerFeature>()?.Error;

    if (exception is not null)
    {
      app.Logger.LogError(
          exception,
          "Erro não tratado em {Method} {Path}",
          context.Request.Method,
          context.Request.Path);
    }

    var detail = app.Environment.IsDevelopment()
        ? exception?.Message
        : "Ocorreu um erro inesperado. Tente novamente mais tarde.";

    await Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Erro interno do servidor",
        detail: detail,
        instance: context.Request.Path)
        .ExecuteAsync(context);
  });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "Healthy",
        service = "VehicleBilling.Api",
        timestamp = DateTimeOffset.UtcNow
    }))
    .WithName("HealthCheck")
    .WithTags("Health");

app.MapGet(
    "/health/database",
    async (
        VehicleBillingDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var canConnect = await dbContext.Database
            .CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Banco de dados indisponÃƒÂ­vel");
        }

        return Results.Ok(new
        {
            status = "Healthy",
            database = "PostgreSQL",
            timestamp = DateTimeOffset.UtcNow
        });
    })
    .WithName("DatabaseHealthCheck")
    .WithTags("Health");

app.MapCustomerEndpoints();
app.MapOperationEndpoints();
app.MapInvoiceEndpoints();
app.MapStatementEndpoints();

app.Run();

