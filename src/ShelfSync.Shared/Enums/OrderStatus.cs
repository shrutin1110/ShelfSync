namespace ShelfSync.Shared.Enums;

// An enum is a fixed set of named values
// Instead of storing "Pending" as a raw string (typos possible)
// or 0, 1, 2 as integers (unreadable)
// you use OrderStatus.Pending — the compiler catches any invalid value

public enum OrderStatus
{
    // Order placed but not yet reviewed
    Pending,

    // Payment verified, order accepted
    Confirmed,

    // Warehouse is picking and packing
    Processing,

    // Package handed to courier
    Shipped,

    // Customer received the package
    Delivered,

    // Order was cancelled at any stage
    Cancelled
}