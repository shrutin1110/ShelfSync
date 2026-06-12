using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ShelfSync.Warehouse.Data;
using ShelfSync.Warehouse.Grpc;

namespace ShelfSync.Warehouse.Services;

// WarehouseServiceBase is AUTO GENERATED from warehouse.proto
// You inherit from it and override the methods
// If you add a new rpc to the proto file
// a new abstract method appears here that you must implement
public class WarehouseGrpcService
    : WarehouseService.WarehouseServiceBase
{
    private readonly WarehouseDbContext _db;
    private readonly ILogger<WarehouseGrpcService> _logger;

    public WarehouseGrpcService(
        WarehouseDbContext db,
        ILogger<WarehouseGrpcService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── RESERVE STOCK ─────────────────────────────────────────
    // Called by Orders service when a new order is placed
    // Checks stock and reserves it atomically
    //
    // ServerCallContext context
    // → contains metadata about the gRPC call
    // → like HttpContext but for gRPC
    // → contains headers, deadline, cancellation token
    public override async Task<ReserveResponse> ReserveStock(
        ReserveRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation(
            "ReserveStock called for product {ProductId}, qty {Qty}",
            request.ProductId, request.Quantity);

        var productId = Guid.Parse(request.ProductId);
        var tenantId = Guid.Parse(request.TenantId);

        // Find the warehouse location for this product
        var location = await _db.WarehouseLocations
            .FirstOrDefaultAsync(w =>
                w.ProductId == productId &&
                w.TenantId == tenantId);

        // Product not in warehouse at all
        if (location is null)
        {
            return new ReserveResponse
            {
                Success = false,
                Message = $"Product {request.ProductId} " +
                          $"not found in warehouse.",
                RemainingQuantity = 0
            };
        }

        // Not enough stock
        if (location.QuantityAvailable < request.Quantity)
        {
            return new ReserveResponse
            {
                Success = false,
                Message = $"Insufficient stock. " +
                          $"Available: {location.QuantityAvailable}, " +
                          $"Requested: {request.Quantity}",
                RemainingQuantity = location.QuantityAvailable
            };
        }

        // Reserve the stock by reducing available quantity
        location.QuantityAvailable -= request.Quantity;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Reserved {Qty} units of {ProductId}. " +
            "Remaining: {Remaining}",
            request.Quantity,
            request.ProductId,
            location.QuantityAvailable);

        return new ReserveResponse
        {
            Success = true,
            Message = "Stock reserved successfully.",
            RemainingQuantity = location.QuantityAvailable
        };
    }

    // ── RELEASE STOCK ─────────────────────────────────────────
    // Called when an order is cancelled
    // Returns reserved stock back to warehouse
    public override async Task<ReleaseResponse> ReleaseStock(
        ReleaseRequest request,
        ServerCallContext context)
    {
        var productId = Guid.Parse(request.ProductId);
        var tenantId = Guid.Parse(request.TenantId);

        var location = await _db.WarehouseLocations
            .FirstOrDefaultAsync(w =>
                w.ProductId == productId &&
                w.TenantId == tenantId);

        if (location is null)
        {
            return new ReleaseResponse
            {
                Success = false,
                Message = "Product not found in warehouse."
            };
        }

        // Return the stock
        location.QuantityAvailable += request.Quantity;
        await _db.SaveChangesAsync();

        return new ReleaseResponse
        {
            Success = true,
            Message = $"Released {request.Quantity} units. " +
                      $"Available: {location.QuantityAvailable}"
        };
    }

    // ── GET INVENTORY LEVEL ───────────────────────────────────
    // Returns current stock level for a product
    public override async Task<InventoryResponse> GetInventoryLevel(
        InventoryRequest request,
        ServerCallContext context)
    {
        var productId = Guid.Parse(request.ProductId);
        var tenantId = Guid.Parse(request.TenantId);

        var location = await _db.WarehouseLocations
            .FirstOrDefaultAsync(w =>
                w.ProductId == productId &&
                w.TenantId == tenantId);

        if (location is null)
        {
            return new InventoryResponse
            {
                ProductId = request.ProductId,
                QuantityAvailable = 0,
                Aisle = "UNKNOWN",
                Shelf = "UNKNOWN"
            };
        }

        return new InventoryResponse
        {
            ProductId = request.ProductId,
            QuantityAvailable = location.QuantityAvailable,
            Aisle = location.Aisle,
            Shelf = location.Shelf
        };
    }

    // ── TRACK PICK AND PACK ───────────────────────────────────
    // Server STREAMING method
    // Client calls once → server streams multiple updates back
    // Simulates the real-time packing process
    //
    // IServerStreamWriter<PackingStatus> responseStream
    // → this is how you write multiple responses back
    // → call responseStream.WriteAsync() for each update
    public override async Task TrackPickPack(
        TrackRequest request,
        IServerStreamWriter<PackingStatus> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation(
            "TrackPickPack started for order {OrderId}",
            request.OrderId);

        // Simulate the pick and pack process
        // In real life these would be triggered by
        // actual warehouse events
        var statuses = new[]
        {
            ("PICKING",       "Warehouse staff locating items"),
            ("PICKED",        "All items collected from shelves"),
            ("PACKING",       "Items being packed into box"),
            ("PACKED",        "Package sealed and labelled"),
            ("READY_TO_SHIP", "Package ready for courier pickup")
        };

        foreach (var (status, message) in statuses)
        {
            // Check if client cancelled the stream
            // context.CancellationToken fires if client disconnects
            if (context.CancellationToken.IsCancellationRequested)
                break;

            // Write one status update to the stream
            // Client receives this immediately
            await responseStream.WriteAsync(new PackingStatus
            {
                OrderId = request.OrderId,
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow.ToString("o")
            });

            _logger.LogInformation(
                "Order {OrderId}: {Status} - {Message}",
                request.OrderId, status, message);

            // Wait 2 seconds between status updates
            // Simulates real warehouse processing time
            await Task.Delay(2000, context.CancellationToken);
        }
    }
}