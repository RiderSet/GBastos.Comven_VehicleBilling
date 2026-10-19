using Comven_VehicleBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddInfrastructure(
    builder.Configuration);

// Application
// builder.Services.AddApplication();

var app = builder.Build();

// Endpoints
app.MapCustomerEndpoints();
app.MapOperationEndpoints();
app.MapStatementEndpoints();
app.MapInvoiceEndpoints();

app.Run();
