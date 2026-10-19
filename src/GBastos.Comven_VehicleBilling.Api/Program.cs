
using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("VehicleBilling")
    ?? throw new InvalidOperationException(
        "A connection string 'VehicleBilling' ainda não foi configurada.");

builder.Services.AddDbContext<VehicleBillingDbContext>(options =>
    options.UseNpgsql(connectionString));

// Constrói a aplicação somente depois de registrar os serviços.
var app = builder.Build();

app.Run();
