using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace ShelfSync.Notifications.Services;

// IHostedService = runs in background for lifetime of app
// BackgroundService = base class that handles the lifecycle
// This continuously polls SQS for new messages
public class OrderCreatedConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    // Set of already-processed message IDs
    // Prevents processing the same message twice
    // This is idempotency — handles SQS at-least-once delivery
    private readonly HashSet<string> _processedMessageIds = new();

    public OrderCreatedConsumer(
        IAmazonSQS sqsClient,
        IConfiguration configuration,
        ILogger<OrderCreatedConsumer> logger)
    {
        _sqsClient = sqsClient;
        _configuration = configuration;
        _logger = logger;
    }

    // ExecuteAsync runs continuously until the app stops
    // stoppingToken fires when app is shutting down
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var queueUrl = _configuration
            ["SQS:OrderCreatedQueueUrl"]!;

        _logger.LogInformation(
            "OrderCreatedConsumer started. " +
            "Polling queue: {QueueUrl}", queueUrl);

        // Keep polling until app shuts down
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndProcessAsync(
                    queueUrl, stoppingToken);
            }
            catch (Exception ex) when
                (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex,
                    "Error polling SQS queue");

                // Wait before retrying to avoid hammering
                // SQS when something is wrong
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task PollAndProcessAsync(
        string queueUrl,
        CancellationToken stoppingToken)
    {
        // ReceiveMessageAsync is a LONG POLL request
        // It waits up to WaitTimeSeconds for messages
        // If no messages arrive in that time → returns empty
        // This is more efficient than constantly hammering SQS
        var receiveRequest = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,

            // Get up to 10 messages per poll
            // SQS maximum is 10
            MaxNumberOfMessages = 10,

            // Long poll — wait up to 20 seconds for messages
            // Reduces empty responses and API costs
            WaitTimeSeconds = 20,

            // How long to hide message from other consumers
            // while this consumer is processing it
            // If processing takes longer than this → message
            // becomes visible again and another consumer picks it up
            VisibilityTimeout = 30,

            // Also receive message attributes
            MessageAttributeNames = new List<string> { "All" }
        };

        var response = await _sqsClient
            .ReceiveMessageAsync(receiveRequest, stoppingToken);

        if (response.Messages is null || !response.Messages.Any())
            return;

        foreach (var message in response.Messages)
        {
            await ProcessMessageAsync(
                queueUrl, message, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(
        string queueUrl,
        Message message,
        CancellationToken stoppingToken)
    {
        // IDEMPOTENCY CHECK
        // SQS guarantees at-least-once delivery
        // The same message CAN be delivered more than once
        // We track processed message IDs to skip duplicates
        if (_processedMessageIds.Contains(message.MessageId))
        {
            _logger.LogWarning(
                "Duplicate message detected: {MessageId}. " +
                "Skipping.", message.MessageId);

            // Still delete it from the queue
            await DeleteMessageAsync(queueUrl, message);
            return;
        }

        try
        {
            _logger.LogInformation(
                "Processing message: {MessageId}",
                message.MessageId);

            // Deserialize the JSON message body
            var orderEvent = JsonSerializer.Deserialize
                <OrderCreatedEventMessage>(
                    message.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (orderEvent is not null)
            {
                // Process the event
                await HandleOrderCreatedAsync(orderEvent);
            }

            // Mark as processed to prevent duplicate processing
            _processedMessageIds.Add(message.MessageId);

            // Delete message from queue — processed successfully
            // If you do not delete → becomes visible again after
            // visibility timeout and gets processed again
            await DeleteMessageAsync(queueUrl, message);

            _logger.LogInformation(
                "Successfully processed message: {MessageId}",
                message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process message: {MessageId}",
                message.MessageId);

            // Do NOT delete the message on failure
            // After visibility timeout it becomes visible again
            // After MaxReceives attempts → goes to DLQ
        }
    }

    private async Task HandleOrderCreatedAsync(
        OrderCreatedEventMessage orderEvent)
    {
        // In a real app you would send an email here via SES
        // For now we log the event details
        _logger.LogInformation(
            "ORDER CREATED EVENT RECEIVED:\n" +
            "  OrderId:     {OrderId}\n" +
            "  TenantId:    {TenantId}\n" +
            "  TotalAmount: {TotalAmount}\n" +
            "  Items:       {ItemCount}",
            orderEvent.OrderId,
            orderEvent.TenantId,
            orderEvent.TotalAmount,
            orderEvent.Items?.Count ?? 0);

        // Simulate sending email
        await Task.Delay(100);

        _logger.LogInformation(
            "Confirmation email sent for order {OrderId}",
            orderEvent.OrderId);
    }

    private async Task DeleteMessageAsync(
        string queueUrl, Message message)
    {
        await _sqsClient.DeleteMessageAsync(
            queueUrl, message.ReceiptHandle);
    }
}

// Message shape matching what Orders service publishes
public record OrderCreatedEventMessage(
    Guid EventId,
    Guid OrderId,
    Guid TenantId,
    Guid UserId,
    decimal TotalAmount,
    DateTime CreatedAt,
    List<OrderItemMessage>? Items
);

public record OrderItemMessage(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);