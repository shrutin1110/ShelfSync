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

            var message = JsonSerializer.Deserialize<InvoiceMessage>(
                record.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (message is null)
            {
                context.Logger.LogError("Failed to deserialize message");
                return;
            }

            await GenerateInvoiceAsync(message, context);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            throw;
        }
    }

    private async Task GenerateInvoiceAsync(
        InvoiceMessage message,
        ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"Generating invoice for order {message.OrderId} " +
            $"tenant: {message.TenantName}");

        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                // ── HEADER ────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        // Left: company name + subtitle
                        row.RelativeItem().Column(c =>
                        {
                            c.Item()
                                .Text(message.TenantName ?? "ShelfSync Store")
                                .FontSize(24)
                                .Bold()
                                .FontColor(Colors.Black);

                            c.Item()
                                .Text("Order Invoice")
                                .FontSize(13)
                                .FontColor(Colors.Grey.Darken1);
                        });

                        // Right: INVOICE badge
                        row.ConstantItem(110)
                            .AlignRight()
                            .Background(Colors.Blue.Darken2)
                            .Padding(10)
                            .Text("INVOICE")
                            .FontSize(16)
                            .Bold()
                            .FontColor(Colors.White);
                    });

                    col.Item().PaddingTop(10)
                        .LineHorizontal(2)
                        .LineColor(Colors.Grey.Lighten2);
                });

                // ── CONTENT ───────────────────────────────────────
                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(16);

                    // Order details row
                    col.Item()
                        .Background(Colors.Grey.Lighten4)
                        .Padding(14)
                        .Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item()
                                    .Text("ORDER ID")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);
                                c.Item()
                                    .Text($"#{message.OrderId.ToString()[..8].ToUpper()}")
                                    .FontSize(14)
                                    .Bold();
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item()
                                    .Text("DATE")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);
                                c.Item()
                                    .Text(message.CreatedAt.ToString("MMMM dd, yyyy"))
                                    .Bold();
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item()
                                    .Text("STATUS")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);
                                c.Item()
                                    .Text("CONFIRMED")
                                    .Bold()
                                    .FontColor(Colors.Green.Darken2);
                            });
                        });

                    // Items table header
                    col.Item().Text("Order Items").FontSize(13).Bold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3); // Product name
                            cols.RelativeColumn(1); // Qty
                            cols.RelativeColumn(1); // Unit price
                            cols.RelativeColumn(1); // Subtotal
                        });

                        // Table header
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Blue.Darken2)
                             .Padding(8);

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell)
                                .Text("Product").FontColor(Colors.White).Bold();
                            header.Cell().Element(HeaderCell)
                                .AlignCenter()
                                .Text("Qty").FontColor(Colors.White).Bold();
                            header.Cell().Element(HeaderCell)
                                .AlignRight()
                                .Text("Unit Price").FontColor(Colors.White).Bold();
                            header.Cell().Element(HeaderCell)
                                .AlignRight()
                                .Text("Subtotal").FontColor(Colors.White).Bold();
                        });

                        // Table rows
                        var items = message.Items ?? new List<InvoiceItem>();
                        for (var i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            var subtotal = item.Quantity * item.UnitPrice;
                            var bg = i % 2 == 0
                                ? Colors.White
                                : Colors.Grey.Lighten4;

                            static IContainer DataCell(
                                IContainer c, string color) =>
                                c.Background(color).Padding(8);

                            table.Cell().Element(c => DataCell(c, bg))
                                .Text(item.ProductName ?? "Product");
                            table.Cell().Element(c => DataCell(c, bg))
                                .AlignCenter()
                                .Text(item.Quantity.ToString());
                            table.Cell().Element(c => DataCell(c, bg))
                                .AlignRight()
                                .Text($"${item.UnitPrice:F2}");
                            table.Cell().Element(c => DataCell(c, bg))
                                .AlignRight()
                                .Text($"${subtotal:F2}").Bold();
                        }
                    });

                    // Total
                    col.Item().AlignRight().Row(row =>
                    {
                        row.ConstantItem(220)
                            .Background(Colors.Blue.Darken2)
                            .Padding(12)
                            .Row(r =>
                            {
                                r.RelativeItem()
                                    .Text("TOTAL")
                                    .FontColor(Colors.White)
                                    .Bold()
                                    .FontSize(14);
                                r.AutoItem()
                                    .Text($"${message.TotalAmount:F2}")
                                    .FontColor(Colors.White)
                                    .Bold()
                                    .FontSize(14);
                            });
                    });

                    // Notes if present
                    if (!string.IsNullOrEmpty(message.Notes))
                    {
                        col.Item().Column(notes =>
                        {
                            notes.Item()
                                .Text("Notes")
                                .Bold()
                                .FontSize(12);
                            notes.Item()
                                .Background(Colors.Grey.Lighten4)
                                .Padding(10)
                                .Text(message.Notes)
                                .FontColor(Colors.Grey.Darken2);
                        });
                    }
                });

                // ── FOOTER ────────────────────────────────────────
                page.Footer().AlignCenter().Column(col =>
                {
                    col.Item()
                        .LineHorizontal(1)
                        .LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(6).Text(text =>
                    {
                        text.Span("Generated by ShelfSync  |  ")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                        text.Span(message.TenantName ?? "")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                        text.Span("  |  Page ")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                        text.CurrentPageNumber()
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }).GeneratePdf();

        context.Logger.LogInformation(
            $"PDF generated. Size: {pdfBytes.Length} bytes");

        var s3Key = $"invoices/{message.TenantId}/{message.OrderId}.pdf";

        using var stream = new MemoryStream(pdfBytes);

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            InputStream = stream,
            ContentType = "application/pdf"
        });

        context.Logger.LogInformation(
            $"Invoice uploaded to S3: {s3Key}");
    }
}

// Updated message shape — now includes full order details
public record InvoiceMessage(
    Guid EventId,
    Guid OrderId,
    Guid TenantId,
    string TenantName,
    decimal TotalAmount,
    DateTime CreatedAt,
    string? Notes,
    List<InvoiceItem>? Items
);

public record InvoiceItem(
    string ProductName,
    int Quantity,
    decimal UnitPrice
);