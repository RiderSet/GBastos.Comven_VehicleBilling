using Comven_VehicleBilling.Api.Endpoints;
using Comven_VehicleBilling.Application;
using Comven_VehicleBilling.Infrastructure;
using Comven_VehicleBilling.Infrastructure.Persistence;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// 1. CONFIGURATION
// ======================================================

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

// ======================================================
// 2. SERVIÃ‡OS DA API
// ======================================================

// Swagger / API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Tratamento padronizado de erros
builder.Services.AddProblemDetails();

// Health checks
builder.Services.AddHealthChecks();

// ======================================================
// 3. CORS
// ======================================================

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

// ======================================================
// 4. DEPENDENCY INJECTION
// ======================================================

// Camada Application:
// casos de uso e serviÃ§os da aplicaÃ§Ã£o.
builder.Services.AddApplication();

// Camada Infrastructure:
// banco de dados, repositÃ³rios e integraÃ§Ãµes.
builder.Services.AddInfrastructure(builder.Configuration);

// ======================================================
// 5. APPLICATION BUILD
// ======================================================

var app = builder.Build();

// ======================================================
// 6. GLOBAL EXCEPTION HANDLING
// ======================================================

app.UseExceptionHandler(errorApp =>
{
  errorApp.Run(async context =>
  {
    var exceptionFeature =
        context.Features.Get<IExceptionHandlerFeature>();

    var exception = exceptionFeature?.Error;

    var detail = app.Environment.IsDevelopment()
        ? exception?.Message
        : "Ocorreu um erro inesperado. Tente novamente mais tarde.";

    await Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Erro interno do servidor",
        detail: detail,
        instance: context.Request.Path
    ).ExecuteAsync(context);
  });
});

// ======================================================
// 7. SWAGGER
// ======================================================

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

// ======================================================
// 8. MIDDLEWARES HTTP
// ======================================================

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

// ======================================================
// 9. HEALTH ENDPOINTS
// ======================================================

// GET /health
// Checks whether the API is responding.
app.MapGet("/health", () =>
{
  return Results.Ok(new
  {
    status = "Healthy",
    service = "VehicleBilling.Api",
    timestamp = DateTimeOffset.UtcNow
  });
})
.WithName("HealthCheck")
.WithTags("Health");

// GET /health/database
// Verifica se a API consegue conectar ao banco.
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
            title: "Banco de dados indisponÃ­vel");
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

// ======================================================
// 10. BUSINESS ENDPOINTS
// ======================================================

// Enable only if these extension methods exist.
// existirem com estes nomes no seu projeto.
//
app.MapCustomerEndpoints();
app.MapOperationEndpoints();
// app.MapStatementEndpoints();
// app.MapInvoiceEndpoints();

// ======================================================
// 11. EXECUTION
// ======================================================

app.Run();
