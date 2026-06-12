using Grpc.Net.Client;
using ShelfSync.Warehouse.Grpc;

namespace ShelfSync.Orders.Services;

// Renamed from WarehouseService to WarehouseGrpcClient
// to avoid conflict with the generated gRPC class
// which is also called WarehouseService
public class WarehouseGrpcClient : IWarehouseService
{
    private readonly WarehouseService.WarehouseServiceClient _client;
    private readonly ILogger<WarehouseGrpcClient> _logger;

    public WarehouseGrpcClient(
        IConfiguration configuration,
        ILogger<WarehouseGrpcClient> logger)
    {
        _logger = logger;

        var warehouseUrl = configuration
            ["GrpcSettings:WarehouseServiceUrl"]!;
        // AppContext.SetSwitch is required for insecure HTTP/2
        // gRPC over plain HTTP without TLS
        // In production you would use HTTPS and this is not needed
        AppContext.SetSwitch(
            "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",
            true);

        var channel = GrpcChannel.ForAddress(warehouseUrl);

        _client = new WarehouseService
            .WarehouseServiceClient(channel);
    }

    public async Task<StockReservationResult> ReserveStockAsync(
        Guid productId,
        Guid tenantId,
        int quantity,
        Guid orderId)
    {
        try
        {
            _logger.LogInformation(
                "Calling Warehouse gRPC ReserveStock. " +
                "ProductId: {ProductId}, Qty: {Qty}",
                productId, quantity);

            var response = await _client.ReserveStockAsync(
                new ReserveRequest
                {
                    ProductId = productId.ToString(),
                    TenantId = tenantId.ToString(),
                    Quantity = quantity,
                    OrderId = orderId.ToString()
                });

            return new StockReservationResult(
                response.Success,
                response.Message,
                response.RemainingQuantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "gRPC call to Warehouse failed");

            return new StockReservationResult(
                false,
                $"Warehouse service unavailable: {ex.Message}",
                0);
        }
    }

    public async Task<bool> ReleaseStockAsync(
        Guid productId,
        Guid tenantId,
        int quantity,
        Guid orderId)
    {
        try
        {
            var response = await _client.ReleaseStockAsync(
                new ReleaseRequest
                {
                    ProductId = productId.ToString(),
                    TenantId = tenantId.ToString(),
                    Quantity = quantity,
                    OrderId = orderId.ToString()
                });

            return response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC ReleaseStock failed");
            return false;
        }
    }

    public async Task<int> GetInventoryLevelAsync(
        Guid productId,
        Guid tenantId)
    {
        try
        {
            var response = await _client.GetInventoryLevelAsync(
                new InventoryRequest
                {
                    ProductId = productId.ToString(),
                    TenantId = tenantId.ToString()
                });

            return response.QuantityAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "gRPC GetInventoryLevel failed");
            return 0;
        }
    }
}