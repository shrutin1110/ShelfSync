using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using ShelfSync.Orders.Events;
using ShelfSync.Orders.Settings;

namespace ShelfSync.Orders.Services;
public record InvoiceItemDto(
    string ProductName,
    int Quantity,
    decimal UnitPrice
);

public class SqsPublisher : ISqsPublisher
{
    private readonly IAmazonSQS _sqsClient;
    private readonly SqsSettings _settings;
    private readonly ILogger<SqsPublisher> _logger;

    public SqsPublisher(
        IAmazonSQS sqsClient,
        IOptions<SqsSettings> settings,
        ILogger<SqsPublisher> logger)
    {
        _sqsClient = sqsClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishOrderCreatedAsync(
        OrderCreatedEvent orderEvent)
    {
        await PublishMessageAsync(
            _settings.OrderCreatedQueueUrl,
            orderEvent,
            "OrderCreated");
    }

    public async Task PublishOrderShippedAsync(
        OrderShippedEvent shippedEvent)
    {
        await PublishMessageAsync(
            _settings.OrderShippedQueueUrl,
            shippedEvent,
            "OrderShipped");
    }

    public async Task PublishInvoiceGenerateAsync(
        Guid orderId,
        Guid tenantId,
        string tenantName,
        decimal totalAmount,
        DateTime createdAt,
        string? notes,
        List<InvoiceItemDto> items)
    {
        var message = new
        {
            EventId = Guid.NewGuid(),
            OrderId = orderId,
            TenantId = tenantId,
            TenantName = tenantName,
            TotalAmount = totalAmount,
            CreatedAt = createdAt,
            Notes = notes,
            Items = items
        };

        await PublishMessageAsync(
            _settings.InvoiceGenerateQueueUrl,
            message,
            "InvoiceGenerate");
    }

    // Generic publish method used by all public methods above
    // Serializes any object to JSON and sends to SQS
    private async Task PublishMessageAsync<T>(
        string queueUrl,
        T message,
        string eventType)
    {
        try
        {
            // Serialize message to JSON
            // This is what gets stored in SQS and sent to consumers
            var messageBody = JsonSerializer.Serialize(message,
                new JsonSerializerOptions
                {
                    // Use camelCase for JSON properties
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                });

            var request = new SendMessageRequest
            {
                // Which queue to send to
                QueueUrl = queueUrl,

                // The message payload
                MessageBody = messageBody,

                // Message attributes are metadata about the message
                // They are separate from the body
                // Useful for filtering without deserializing the body
                MessageAttributes = new Dictionary<string,
                    MessageAttributeValue>
                {
                    // EventType attribute lets consumers quickly
                    // identify what kind of event this is
                    // without parsing the JSON body
                    ["EventType"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = eventType
                    },

                    // Track which service published this message
                    // Useful for debugging
                    ["Source"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = "ShelfSync.Orders"
                    }
                }
            };

            var response = await _sqsClient
                .SendMessageAsync(request);

            _logger.LogInformation(
                "Published {EventType} event to SQS. " +
                "MessageId: {MessageId}",
                eventType, response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish {EventType} to SQS",
                eventType);

            // Re-throw so the caller knows publishing failed
            // The order was saved but the event was not published
            // This is a consistency issue to handle carefully
            throw;
        }
    }
}