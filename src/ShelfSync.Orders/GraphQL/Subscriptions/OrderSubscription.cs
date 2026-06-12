using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.GraphQL.Subscriptions;

// Subscriptions enable real-time updates via WebSocket
// Client subscribes once and receives updates automatically
[SubscriptionType]
public class OrderSubscription
{
    // OnOrderStatusChanged is the subscription field name
    // in the GraphQL schema it becomes: orderStatusChanged
    //
    // [Subscribe] tells Hot Chocolate this is a subscription handler
    // [Topic] is the event channel name
    // When mutation publishes to "OrderStatusChanged" topic
    // this subscription fires automatically
    [Subscribe]
    [Topic("OrderStatusChanged")]
    public Order OnOrderStatusChanged(
        // [EventMessage] is the data published to this topic
        // It is the Order object the mutation published
        [EventMessage] Order order) => order;
}