using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using ShelfSync.Notifications.Services;

var builder = WebApplication.CreateBuilder(args);

// ── AWS CREDENTIALS ───────────────────────────────────────────
var awsOptions = builder.Configuration.GetAWSOptions();
awsOptions.Credentials = new BasicAWSCredentials(
    builder.Configuration["AWS:AccessKeyId"],
    builder.Configuration["AWS:SecretAccessKey"]);
awsOptions.Region = RegionEndpoint.GetBySystemName(
    builder.Configuration["AWS:Region"] ?? "us-east-1");

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonSQS>();

// ── SQS CONSUMERS ─────────────────────────────────────────────
// AddHostedService registers a background service
// It starts automatically when the app starts
// and runs until the app stops
builder.Services.AddHostedService<OrderCreatedConsumer>();

var app = builder.Build();

app.MapGet("/", () => "Notifications service is running.");

app.Run();