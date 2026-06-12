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
// Configure Kestrel to use HTTP/2 for gRPC
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP/2 without TLS — for development only
    // Production should use HTTPS with HTTP/2
    options.ListenLocalhost(5003, listenOptions =>
    {
        listenOptions.Protocols =
            Microsoft.AspNetCore.Server.Kestrel.Core
                .HttpProtocols.Http2;
    });
});

var app = builder.Build();



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