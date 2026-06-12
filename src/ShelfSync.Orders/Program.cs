using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShelfSync.Orders.Data;
using ShelfSync.Orders.GraphQL.Mutations;
using ShelfSync.Orders.GraphQL.Queries;
using ShelfSync.Orders.GraphQL.Subscriptions;
using ShelfSync.Shared.Interfaces;
using ShelfSync.Shared.Middleware;
using System.Text;
using ShelfSync.Orders.DataLoaders;
using ShelfSync.Orders.Services;

var builder = WebApplication.CreateBuilder(args);

// ── 1. DATABASE ───────────────────────────────────────────────
// AddDbContextFactory in addition to AddDbContext
// AddDbContext         → for regular service injection (Scoped)
// AddDbContextFactory  → for DataLoader (creates fresh contexts per batch)
// Both are needed — they serve different purposes
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContextFactory<OrdersDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")),
    ServiceLifetime.Scoped);

// ── 2. TENANT CONTEXT ─────────────────────────────────────────
builder.Services.AddScoped<ITenantContext, TenantContextBase>();

// ── 3. JWT AUTHENTICATION ─────────────────────────────────────
var secretKey = builder.Configuration["JwtSettings:SecretKey"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine(
                    $"TOKEN FAILED: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("TOKEN VALIDATED SUCCESSFULLY");
                return Task.CompletedTask;
            },
            // This is the key one for GraphQL
            // GraphQL sends requests differently than REST
            // This event fires when the token is being read
            OnMessageReceived = context =>
            {
                Console.WriteLine(
                    $"AUTH HEADER: {context.Request.Headers["Authorization"]}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
// Register warehouse gRPC client service
// Scoped because it is used per request
builder.Services.AddScoped<IWarehouseService, WarehouseGrpcClient>();
builder.Services.AddHttpContextAccessor();

// ── 4. GRAPHQL ────────────────────────────────────────────────
builder.Services
    .AddGraphQLServer()
    .AddQueryType()
    .AddTypeExtension<OrderQuery>()
    .AddTypeExtension<ProductQuery>()
    .AddMutationType()
    .AddTypeExtension<OrderMutation>()
    .AddSubscriptionType()
    .AddTypeExtension<OrderSubscription>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddInMemorySubscriptions()
    .AddAuthorization()
    .ModifyRequestOptions(opt =>
        opt.IncludeExceptionDetails = true)
    .AddDataLoader<ProductDataLoader>()      // ← add this
    .AddDataLoader<OrderItemDataLoader>();  


// ── 5. CORS ───────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets();
app.MapGraphQL();
app.Run();