using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson
        .DefaultLambdaJsonSerializer))]

namespace ShelfSync.Lambda.InvoiceGenerator;

public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public Function()
    {
        // AmazonS3Client automatically reads AWS_REGION
        // that Lambda injects — no need to specify it manually
        _s3Client = new AmazonS3Client();

        _bucketName = Environment
                          .GetEnvironmentVariable("S3_BUCKET_NAME")
                      ?? throw new Exception("S3_BUCKET_NAME not set");
    }

    public async Task FunctionHandler(
        SQSEvent sqsEvent,
        ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"Processing {sqsEvent.Records.Count} messages");

        foreach (var record in sqsEvent.Records)
        {
            await ProcessRecordAsync(record, context);
        }
    }

    private async Task ProcessRecordAsync(
        SQSEvent.SQSMessage record,
        ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation(
                $"Processing message: {record.MessageId}");

            var message = JsonSerializer.Deserialize
                <InvoiceMessage>(
                    record.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (message is null)
            {
                context.Logger.LogError(
                    "Failed to deserialize message body");
                return;
            }

            await GenerateInvoiceAsync(
                message.OrderId,
                message.TenantId,
                context);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                $"Error: {ex.Message}");
            throw;
        }
    }

    private async Task GenerateInvoiceAsync(
        Guid orderId,
        Guid tenantId,
        ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"Generating invoice for order {orderId}");

        // Generate PDF without database lookup
        // Full order details will be added on Day 18
        // when RDS PostgreSQL is configured
        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                // ── HEADER ────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item()
                        .Text("INVOICE")
                        .FontSize(32)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    col.Item()
                        .Text(
                            $"Order #" +
                            $"{orderId.ToString()[..8].ToUpper()}")
                        .FontSize(16)
                        .FontColor(Colors.Grey.Darken1);

                    col.Item()
                        .Text(
                            $"Date: " +
                            $"{DateTime.UtcNow:MMMM dd, yyyy}")
                        .FontSize(11);
                });

                // ── CONTENT ───────────────────────────────
                page.Content().Column(col =>
                {
                    col.Spacing(20);

                    // Order info box
                    col.Item()
                        .Background(Colors.Grey.Lighten3)
                        .Padding(15)
                        .Column(inner =>
                        {
                            inner.Item()
                                .Text("Order Information")
                                .FontSize(13)
                                .Bold();

                            inner.Spacing(5);

                            inner.Item()
                                .Text($"Order ID: {orderId}")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken2);

                            inner.Item()
                                .Text($"Tenant ID: {tenantId}")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken2);

                            inner.Item()
                                .Text(
                                    $"Generated: " +
                                    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken2);
                        });

                    // Status box
                    col.Item()
                        .Background(Colors.Green.Lighten4)
                        .Padding(15)
                        .Column(inner =>
                        {
                            inner.Item()
                                .Text("✓ Order Confirmed")
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Green.Darken2);

                            inner.Item()
                                .Text(
                                    "Your order has been received " +
                                    "and is being processed.")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken2);
                        });

                    // Note about full details
                    col.Item()
                        .Border(1)
                        .BorderColor(Colors.Blue.Lighten2)
                        .Padding(10)
                        .Text(
                            "Full order details including items, " +
                            "quantities and pricing are available " +
                            "in your account dashboard.")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                });

                // ── FOOTER ────────────────────────────────
                page.Footer()
                    .AlignCenter()
                    .Text(
                        "ShelfSync — Multi-Tenant Order Management Platform")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        context.Logger.LogInformation(
            $"PDF generated successfully. " +
            $"Size: {pdfBytes.Length} bytes");

        // Upload PDF to S3
        var s3Key = $"invoices/{tenantId}/{orderId}.pdf";

        using var stream = new MemoryStream(pdfBytes);

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            InputStream = stream,
            ContentType = "application/pdf"
        });

        context.Logger.LogInformation(
            $"Invoice uploaded to S3 at: {s3Key}");

        context.Logger.LogInformation(
            "Invoice generation complete. " +
            "Database record will be saved after RDS setup on Day 18.");
    }
}

// Message shape from SQS
public record InvoiceMessage(
    Guid EventId,
    Guid OrderId,
    Guid TenantId,
    DateTime RequestedAt
);