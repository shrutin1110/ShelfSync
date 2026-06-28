import { useState,useEffect, useCallback } from 'react';
import Layout from '../components/Layout';
import getClient from '../api/apolloClient';
import { useSubscription } from '../hooks/useSubscription';

// ── GraphQL Operations ────────────────────────────────────────

const GET_ORDERS = `
  query {
    orders{
      id
      status
      totalAmount
      notes
      createdAt
      updatedAt
      items {
        id
        quantity
        unitPrice
        product {
          id
          name
          sku
        }
      }
    }
  }
`;

const UPDATE_ORDER_STATUS = `
  mutation UpdateStatus($input: UpdateOrderStatusInput!) {
    updateOrderStatus(input: $input) {
      id
      status
      updatedAt
    }
  }
`;

// Subscription — server pushes this when any order changes
const ORDER_STATUS_CHANGED = `
  subscription {
    onOrderStatusChanged {
      id
      status
      updatedAt
    }
  }
`;

// ── Types ─────────────────────────────────────────────────────

interface OrderItem {
    id: string;
    quantity: number;
    unitPrice: number;
    product: {
        id: string;
        name: string;
        sku: string;
    };
}

interface Order {
    id: string;
    status: string;
    totalAmount: number;
    notes: string;
    createdAt: string;
    updatedAt: string;
    items: OrderItem[];
}

interface OrderStatusUpdate {
    onOrderStatusChanged: {
        id: string;
        status: string;
        updatedAt: string;
    };
}

const STATUS_TRANSITIONS: Record<string, string[]> = {
    PENDING:    ['CONFIRMED', 'CANCELLED'],
    CONFIRMED:  ['PROCESSING', 'CANCELLED'],
    PROCESSING: ['SHIPPED'],
    SHIPPED:    ['DELIVERED'],
    DELIVERED:  [],
    CANCELLED:  []
};

const STATUS_COLORS: Record<string, {
    bg: string; color: string
}> = {
    PENDING:    { bg: '#fef3c7', color: '#92400e' },
    CONFIRMED:  { bg: '#dbeafe', color: '#1e40af' },
    PROCESSING: { bg: '#e0e7ff', color: '#3730a3' },
    SHIPPED:    { bg: '#d1fae5', color: '#065f46' },
    DELIVERED:  { bg: '#dcfce7', color: '#14532d' },
    CANCELLED:  { bg: '#fee2e2', color: '#991b1b' }
};

// ── Main Component ────────────────────────────────────────────

export default function OrdersPage() {
    const [orders, setOrders] = useState<Order[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [activeFilter, setActiveFilter] = useState('All');
    const [expandedOrderId, setExpandedOrderId] =
        useState<string | null>(null);
    const [updatingOrderId, setUpdatingOrderId] =
        useState<string | null>(null);
    const [successMessage, setSuccessMessage] =
        useState<string | null>(null);
    const [lastUpdate, setLastUpdate] =
        useState<string | null>(null);

    const filters = [
        'All', 'PENDING', 'CONFIRMED',
        'PROCESSING', 'SHIPPED', 'DELIVERED', 'CANCELLED'
    ];

    const loadOrders = useCallback(async () => {
        console.log('loadOrders called');
        try {
            console.log('making request...');
            const result = await getClient()
                .request<{ orders: Order[] }>(GET_ORDERS);
            console.log('result received:', result);
            console.log('orders count:', result.orders?.length);

            const sorted = [...result.orders].sort(
                (a, b) =>
                    new Date(b.createdAt).getTime() -
                    new Date(a.createdAt).getTime()
            );

            console.log('sorted:', sorted.length);
            setOrders(sorted);
            setError(null);
        } catch (err: any) {
            console.error('loadOrders error:', err);
            setError('Failed to load orders.');
        } finally {
            console.log('setting loading false');
            setLoading(false);
        }
    }, []);

    // Load orders on mount
    useEffect(() => {
        loadOrders();
    }, []);

    // ── WebSocket Subscription ────────────────────────────────
    // This replaces polling completely
    // Server pushes updates the moment an order status changes
    const { isConnected } = useSubscription<OrderStatusUpdate>({
        query: ORDER_STATUS_CHANGED,

        onData: (data) => {
            const updatedOrder = data.onOrderStatusChanged;

            console.log(
                'Real-time update received:',
                updatedOrder.id,
                updatedOrder.status
            );

            // Update ONLY the changed order in state
            // Do not reload all orders — just patch the one that changed
            // This is more efficient and avoids flickering
            setOrders(prev =>
                prev.map(order =>
                    order.id === updatedOrder.id
                        ? {
                            ...order,               // keep all existing fields
                            status: updatedOrder.status,    // update status
                            updatedAt: updatedOrder.updatedAt // update timestamp
                        }
                        : order                     // leave others unchanged
                )
            );

            setLastUpdate(
                `Last update: ${new Date().toLocaleTimeString()}`
            );
        },

        onError: (err) => {
            console.error('Subscription failed:', err);
        }
    });

    // Derived state
    const filteredOrders = activeFilter === 'All'
        ? orders
        : orders.filter(o => o.status === activeFilter);

    const statusCounts = orders.reduce(
        (acc, order) => {
            acc[order.status] = (acc[order.status] ?? 0) + 1;
            return acc;
        },
        {} as Record<string, number>
    );

    const toggleExpand = (orderId: string) => {
        setExpandedOrderId(
            expandedOrderId === orderId ? null : orderId
        );
    };

    const handleUpdateStatus = async (
        orderId: string,
        newStatus: string
    ) => {
        setUpdatingOrderId(orderId);

        try {
            await getClient()
                .request(UPDATE_ORDER_STATUS, {
                    input: { orderId, newStatus }
                });

            // Update state immediately without waiting
            // for WebSocket subscription
            setOrders(prev =>
                prev.map(order =>
                    order.id === orderId
                        ? { ...order, status: newStatus }
                        : order
                )
            );

            setSuccessMessage(
                `Order updated to ${newStatus}`
            );
            setTimeout(() => setSuccessMessage(null), 3000);

        } catch (err: any) {
            console.error('Update status error:', err);
        } finally {
            setUpdatingOrderId(null);
        }
    };

    // ── Render ────────────────────────────────────────────────

    if (loading) return (
        <Layout>
            <div style={styles.container}>
                <div style={styles.loadingState}>
                    Loading orders...
                </div>
            </div>
        </Layout>
    );

    if (error) return (
        <Layout>
            <div style={styles.container}>
                <div style={styles.errorState}>
                    <p>{error}</p>
                    <button onClick={loadOrders} style={styles.retryButton}>
                        Retry
                    </button>
                </div>
            </div>
        </Layout>
    );

    return (
        <Layout>
            <div style={styles.container}>

                {/* Header */}
                <div style={styles.header}>
                    <div>
                        <h1 style={styles.title}>Orders</h1>
                        <p style={styles.subtitle}>
                            {orders.length} total orders
                        </p>
                    </div>
                    <div style={styles.headerRight}>
                        {/* WebSocket connection indicator */}
                        <div style={styles.connectionStatus}>
                            <div style={{
                                ...styles.connectionDot,
                                backgroundColor: isConnected
                                    ? '#10b981' : '#f59e0b'
                            }} />
                            <span style={styles.connectionText}>
                {isConnected
                    ? 'Live updates active'
                    : 'Connecting...'}
              </span>
                        </div>
                        <button
                            onClick={loadOrders}
                            style={styles.refreshButton}
                        >
                            Refresh
                        </button>
                    </div>
                </div>

                {/* Last update timestamp */}
                {lastUpdate && (
                    <div style={styles.lastUpdate}>
                        ⚡ {lastUpdate}
                    </div>
                )}

                {/* Success message */}
                {successMessage && (
                    <div style={styles.success}>
                        {successMessage}
                    </div>
                )}

                {/* Stats row */}
                <div style={styles.statsRow}>
                    {Object.entries(STATUS_COLORS).map(
                        ([status, colors]) => (
                            <div
                                key={status}
                                style={{
                                    ...styles.statChip,
                                    backgroundColor: colors.bg,
                                    color: colors.color
                                }}
                            >
                                {statusCounts[status] ?? 0} {status}
                            </div>
                        )
                    )}
                </div>

                {/* Filter tabs */}
                <div style={styles.filterTabs}>
                    {filters.map(filter => (
                        <button
                            key={filter}
                            onClick={() => setActiveFilter(filter)}
                            style={{
                                ...styles.filterTab,
                                ...(activeFilter === filter
                                    ? styles.filterTabActive : {})
                            }}
                        >
                            {filter}
                            {filter !== 'All' &&
                                statusCounts[filter] &&
                                statusCounts[filter] > 0 && (
                                    <span style={styles.tabCount}>
                  {statusCounts[filter]}
                </span>
                                )}
                        </button>
                    ))}
                </div>

                {/* Orders list */}
                {filteredOrders.length === 0 ? (
                    <div style={styles.emptyState}>
                        {activeFilter === 'All'
                            ? 'No orders yet.'
                            : `No ${activeFilter} orders.`}
                    </div>
                ) : (
                    <div style={styles.ordersList}>
                        {filteredOrders.map(order => (
                            <div key={order.id} style={styles.orderCard}>

                                {/* Order header row */}
                                <div
                                    style={styles.orderHeader}
                                    onClick={() => toggleExpand(order.id)}
                                >
                                    <div style={styles.orderId}>
                                        #{order.id.slice(0, 8).toUpperCase()}
                                    </div>

                                    <span style={{
                                        ...styles.statusBadge,
                                        backgroundColor:
                                            STATUS_COLORS[order.status]?.bg
                                            ?? '#f3f4f6',
                                        color:
                                            STATUS_COLORS[order.status]?.color
                                            ?? '#374151'
                                    }}>
                    {order.status}
                  </span>

                                    <div style={styles.orderTotal}>
                                        ${order.totalAmount.toFixed(2)}
                                    </div>

                                    <div style={styles.itemCount}>
                                        {order.items.length} item
                                        {order.items.length !== 1 ? 's' : ''}
                                    </div>

                                    <div style={styles.orderDate}>
                                        {new Date(order.createdAt)
                                            .toLocaleDateString()}
                                    </div>

                                    <div style={styles.expandArrow}>
                                        {expandedOrderId === order.id
                                            ? '▲' : '▼'}
                                    </div>
                                </div>

                                {/* Expanded details */}
                                {expandedOrderId === order.id && (
                                    <div style={styles.orderDetails}>

                                        <table style={styles.itemsTable}>
                                            <thead>
                                            <tr>
                                                <th style={styles.itemTh}>
                                                    Product
                                                </th>
                                                <th style={styles.itemTh}>SKU</th>
                                                <th style={styles.itemTh}>Qty</th>
                                                <th style={styles.itemTh}>
                                                    Unit Price
                                                </th>
                                                <th style={styles.itemTh}>
                                                    Subtotal
                                                </th>
                                            </tr>
                                            </thead>
                                            <tbody>
                                            {(order.items ?? []).map(item => (
                                                <tr key={item.id}>
                                                    <td style={styles.itemTd}>
                                                        {item.product?.name ?? 'Unknown'}
                                                    </td>
                                                    <td style={styles.itemTd}>
                              <span style={styles.skuBadge}>
                                {item.product?.sku ?? '-'}
                              </span>
                                                    </td>
                                                    <td style={styles.itemTd}>
                                                        {item.quantity}
                                                    </td>
                                                    <td style={styles.itemTd}>
                                                        ${item.unitPrice.toFixed(2)}
                                                    </td>
                                                    <td style={styles.itemTd}>
                                                        <strong>
                                                            ${(item.quantity *
                                                            item.unitPrice
                                                        ).toFixed(2)}
                                                        </strong>
                                                    </td>
                                                </tr>
                                            ))}
                                            </tbody>
                                        </table>

                                        {order.notes && (
                                            <div style={styles.notes}>
                                                <strong>Notes:</strong> {order.notes}
                                            </div>
                                        )}

                                        {STATUS_TRANSITIONS[order.status]
                                            ?.length > 0 && (
                                            <div style={styles.statusActions}>
                        <span style={styles.statusActionsLabel}>
                          Update status:
                        </span>
                                                {STATUS_TRANSITIONS[order.status]
                                                    .map(nextStatus => (
                                                        <button
                                                            key={nextStatus}
                                                            onClick={() =>
                                                                handleUpdateStatus(
                                                                    order.id,
                                                                    nextStatus
                                                                )
                                                            }
                                                            disabled={
                                                                updatingOrderId === order.id
                                                            }
                                                            style={{
                                                                ...styles.statusButton,
                                                                backgroundColor:
                                                                    STATUS_COLORS[nextStatus]
                                                                        ?.bg ?? '#f3f4f6',
                                                                color:
                                                                    STATUS_COLORS[nextStatus]
                                                                        ?.color ?? '#374151',
                                                                opacity:
                                                                    updatingOrderId === order.id
                                                                        ? 0.6 : 1
                                                            }}
                                                        >
                                                            {updatingOrderId === order.id
                                                                ? 'Updating...'
                                                                : `→ ${nextStatus}`}
                                                        </button>
                                                    ))}
                                            </div>
                                        )}

                                        {STATUS_TRANSITIONS[order.status]
                                            ?.length === 0 && (
                                            <div style={styles.terminalState}>
                                                {order.status === 'DELIVERED'
                                                    ? '✓ Order completed'
                                                    : '✗ Order cancelled'}
                                            </div>
                                        )}
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </Layout>
    );
}

// ── Styles ────────────────────────────────────────────────────

const styles: Record<string, React.CSSProperties> = {
    container: {
        padding: '32px',
        maxWidth: '1000px'
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: '24px'
    },
    title: {
        fontSize: '24px',
        fontWeight: '700',
        color: '#1a1a2e',
        margin: '0 0 4px'
    },
    subtitle: {
        color: '#666',
        fontSize: '14px',
        margin: 0
    },
    headerRight: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px'
    },
    connectionStatus: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '6px 12px',
        backgroundColor: '#f9fafb',
        borderRadius: '100px',
        border: '1px solid #e5e7eb'
    },
    connectionDot: {
        width: '8px',
        height: '8px',
        borderRadius: '50%'
    },
    connectionText: {
        fontSize: '12px',
        color: '#6b7280',
        fontWeight: '500'
    },
    refreshButton: {
        padding: '8px 16px',
        backgroundColor: '#f3f4f6',
        border: 'none',
        borderRadius: '8px',
        fontSize: '14px',
        cursor: 'pointer',
        color: '#374151'
    },
    lastUpdate: {
        fontSize: '12px',
        color: '#10b981',
        marginBottom: '16px',
        fontWeight: '500'
    },
    success: {
        backgroundColor: '#d1fae5',
        color: '#065f46',
        padding: '12px 16px',
        borderRadius: '8px',
        marginBottom: '16px',
        fontSize: '14px',
        fontWeight: '500'
    },
    statsRow: {
        display: 'flex',
        gap: '8px',
        flexWrap: 'wrap',
        marginBottom: '20px'
    },
    statChip: {
        padding: '6px 12px',
        borderRadius: '100px',
        fontSize: '13px',
        fontWeight: '500'
    },
    filterTabs: {
        display: 'flex',
        gap: '4px',
        marginBottom: '20px',
        flexWrap: 'wrap'
    },
    filterTab: {
        padding: '8px 16px',
        backgroundColor: '#f3f4f6',
        border: 'none',
        borderRadius: '8px',
        fontSize: '13px',
        cursor: 'pointer',
        color: '#374151',
        display: 'flex',
        alignItems: 'center',
        gap: '6px'
    },
    filterTabActive: {
        backgroundColor: '#4f46e5',
        color: 'white'
    },
    tabCount: {
        backgroundColor: 'rgba(255,255,255,0.3)',
        padding: '1px 6px',
        borderRadius: '100px',
        fontSize: '11px'
    },
    ordersList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px'
    },
    orderCard: {
        backgroundColor: 'white',
        borderRadius: '12px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)',
        overflow: 'hidden'
    },
    orderHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '16px',
        padding: '16px 20px',
        cursor: 'pointer',
        userSelect: 'none'
    },
    orderId: {
        fontFamily: 'monospace',
        fontSize: '13px',
        color: '#6b7280',
        fontWeight: '600',
        minWidth: '100px'
    },
    statusBadge: {
        padding: '4px 10px',
        borderRadius: '100px',
        fontSize: '12px',
        fontWeight: '600',
        minWidth: '90px',
        textAlign: 'center'
    },
    orderTotal: {
        fontWeight: '700',
        color: '#1a1a2e',
        fontSize: '15px',
        flex: 1
    },
    itemCount: {
        color: '#9ca3af',
        fontSize: '13px'
    },
    orderDate: {
        color: '#9ca3af',
        fontSize: '13px'
    },
    expandArrow: {
        color: '#9ca3af',
        fontSize: '12px'
    },
    orderDetails: {
        borderTop: '1px solid #f3f4f6',
        padding: '20px'
    },
    itemsTable: {
        width: '100%',
        borderCollapse: 'collapse',
        marginBottom: '16px'
    },
    itemTh: {
        textAlign: 'left',
        padding: '8px 12px',
        fontSize: '12px',
        fontWeight: '600',
        color: '#6b7280',
        textTransform: 'uppercase',
        borderBottom: '1px solid #e5e7eb'
    },
    itemTd: {
        padding: '10px 12px',
        fontSize: '14px',
        borderBottom: '1px solid #f9fafb',
        color: '#374151'
    },
    skuBadge: {
        fontFamily: 'monospace',
        fontSize: '12px',
        backgroundColor: '#f3f4f6',
        padding: '2px 6px',
        borderRadius: '4px'
    },
    notes: {
        fontSize: '13px',
        color: '#6b7280',
        marginBottom: '16px',
        padding: '10px 12px',
        backgroundColor: '#f9fafb',
        borderRadius: '8px'
    },
    statusActions: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        flexWrap: 'wrap'
    },
    statusActionsLabel: {
        fontSize: '13px',
        color: '#6b7280',
        fontWeight: '500'
    },
    statusButton: {
        padding: '6px 14px',
        border: 'none',
        borderRadius: '8px',
        fontSize: '13px',
        fontWeight: '600',
        cursor: 'pointer'
    },
    terminalState: {
        fontSize: '13px',
        color: '#6b7280',
        fontStyle: 'italic'
    },
    loadingState: {
        padding: '48px',
        textAlign: 'center',
        color: '#6b7280',
        fontSize: '16px'
    },
    errorState: {
        padding: '48px',
        textAlign: 'center',
        color: '#ef4444'
    },
    retryButton: {
        marginTop: '16px',
        padding: '8px 20px',
        backgroundColor: '#4f46e5',
        color: 'white',
        border: 'none',
        borderRadius: '8px',
        cursor: 'pointer'
    },
    emptyState: {
        padding: '48px',
        textAlign: 'center',
        color: '#9ca3af',
        fontSize: '16px',
        backgroundColor: 'white',
        borderRadius: '12px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)'
    }
};