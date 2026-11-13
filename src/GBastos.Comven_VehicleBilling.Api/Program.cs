using Comven_VehicleBilling.Api.Endpoints;
using Comven_VehicleBilling.Application;
using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

// Localiza o .env na raiz do repositório.
static string? FindEnvFile()
{
  var directory = new DirectoryInfo(
      Directory.GetCurrentDirectory());

  while (directory is not null)
  {
    var envPath = Path.Combine(directory.FullName, ".env");

    if (File.Exists(envPath))
      return envPath;

    directory = directory.Parent;
  }

  return null;
}

// Carrega as variáveis antes de configurar a aplicação.
var envFile = FindEnvFile();

if (envFile is not null)
{
  DotNetEnv.Env.Load(envFile);
}
else
{
  throw new FileNotFoundException(
      "Arquivo .env não encontrado na raiz do repositório.");
}

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
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
      try
      {
        await dbContext.Database.OpenConnectionAsync(
            cancellationToken);

        await dbContext.Database.CloseConnectionAsync();

        return Results.Ok(new
        {
          status = "Healthy",
          database = "PostgreSQL",
          timestamp = DateTimeOffset.UtcNow
        });
      }
      catch (Exception ex)
      {
        logger.LogError(
            ex,
            "Falha ao conectar ao PostgreSQL no health check.");

        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Banco de dados indisponível",
            detail: app.Environment.IsDevelopment()
                ? ex.ToString()
                : "Não foi possível conectar ao banco de dados.");
      }
    })
    .WithName("DatabaseHealthCheck")
    .WithTags("Health");

app.MapCustomerEndpoints();
app.MapOperationEndpoints();
app.MapInvoiceEndpoints();
app.MapStatementEndpoints();

app.Run();

