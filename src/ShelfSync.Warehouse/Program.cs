using Microsoft.EntityFrameworkCore;
using ShelfSync.Warehouse.Data;
using ShelfSync.Warehouse.Services;

var builder = WebApplication.CreateBuilder(args);

// ── 1. DATABASE ───────────────────────────────────────────────
builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── 2. GRPC ───────────────────────────────────────────────────
// AddGrpc sets up the gRPC server framework
// This is equivalent to AddControllers() for REST
// but for gRPC services
builder.Services.AddGrpc(options =>
{
    // EnableDetailedErrors shows gRPC error details in development
    // Turn this OFF in production
    options.EnableDetailedErrors =
        builder.Environment.IsDevelopment();
});

var app = builder.Build();

// gRPC requires HTTP/2
// This tells .NET to accept HTTP/2 connections
app.MapGrpcService<WarehouseGrpcService>();

// Seed warehouse data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<WarehouseDbContext>();
    await SeedData.SeedAsync(db);
}

app.MapGrpcService<WarehouseGrpcService>();
app.MapGet("/", () => "Warehouse gRPC service is running.");
app.Run();