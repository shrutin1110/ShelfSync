import { useState, useEffect } from 'react';
import Layout from '../components/Layout';
import { useAuth } from '../auth/AuthContext';
import getClient from '../api/apolloClient';

const DASHBOARD_QUERY = `
  query {
    orders {
      id
      status
      totalAmount
    }
    products {
      id
      stockQuantity
      isActive
    }
  }
`;

interface DashboardData {
    orders: Array<{
        id: string;
        status: string;
        totalAmount: number;
    }>;
    products: Array<{
        id: string;
        stockQuantity: number;
        isActive: boolean;
    }>;
}

export default function DashboardPage() {
    const { user } = useAuth();
    const [data, setData] = useState<DashboardData | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const token = localStorage.getItem('accessToken');
        console.log('Token exists:', !!token);
        console.log('Token preview:', token?.slice(0, 20));

        getClient()
            .request<DashboardData>(DASHBOARD_QUERY)
            .then(result => {
                console.log('Dashboard data:', result);
                setData(result);
                setLoading(false);
            })
            .catch(err => {
                console.error('Dashboard error full:', err);
                console.error('Error message:', err?.message);
                console.error('Error response:', err?.response);
                setError(err?.message ?? JSON.stringify(err));
                setLoading(false);
            });
    }, []);

    if (loading) return (
        <Layout>
            <div style={{ padding: '32px' }}>Loading dashboard...</div>
        </Layout>
    );

    if (error) return (
        <Layout>
            <div style={{ padding: '32px' }}>
                <h2 style={{ color: 'red' }}>Error loading dashboard</h2>
                <pre style={{
                    backgroundColor: '#fff0f0',
                    padding: '16px',
                    borderRadius: '8px',
                    fontSize: '12px',
                    overflow: 'auto',
                    whiteSpace: 'pre-wrap'
                }}>
          {error}
        </pre>
                <p style={{ marginTop: '12px', color: '#666' }}>
                    Make sure the Orders service is running on port 5002.
                </p>
            </div>
        </Layout>
    );


    const orders = data?.orders ?? [];
    const products = data?.products ?? [];

    const totalRevenue = orders
        .filter(o => o.status !== 'Cancelled')
        .reduce((sum, o) => sum + o.totalAmount, 0);

    const pendingOrders = orders
        .filter(o => o.status === 'Pending').length;

    const lowStockProducts = products
        .filter(p => p.stockQuantity < 10).length;

    return (
        <Layout>
            <div style={styles.container}>
                <div style={styles.header}>
                    <h1 style={styles.title}>
                        Welcome back, {user?.email?.split('@')[0] ?? 'there'}
                    </h1>
                    <p style={styles.subtitle}>
                        {user?.tenantName ?? ''} — {user?.role ?? ''}
                    </p>
                </div>

                {loading ? (
                    <p>Loading stats...</p>
                ) : (
                    <div style={styles.statsGrid}>
                        <StatCard
                            title="Total Orders"
                            value={orders.length}
                            icon="🛒"
                            color="#4f46e5"
                        />
                        <StatCard
                            title="Pending Orders"
                            value={pendingOrders}
                            icon="⏳"
                            color="#f59e0b"
                        />
                        <StatCard
                            title="Total Products"
                            value={products.length}
                            icon="📦"
                            color="#10b981"
                        />
                        <StatCard
                            title="Low Stock Items"
                            value={lowStockProducts}
                            icon="⚠️"
                            color="#ef4444"
                        />
                        <StatCard
                            title="Total Revenue"
                            value={`$${totalRevenue.toFixed(2)}`}
                            icon="💰"
                            color="#8b5cf6"
                        />
                    </div>
                )}

                <div style={styles.section}>
                    <h2 style={styles.sectionTitle}>Recent Orders</h2>
                    {orders.length === 0 ? (
                        <p style={styles.empty}>No orders yet.</p>
                    ) : (
                        <div style={styles.orderList}>
                            {orders.slice(0, 5).map(order => (
                                <div key={order.id} style={styles.orderRow}>
                  <span style={styles.orderId}>
                    #{order.id.slice(0, 8).toUpperCase()}
                  </span>
                                    <span style={{
                                        ...styles.statusBadge,
                                        backgroundColor: getStatusColor(order.status)
                                    }}>
                    {order.status}
                  </span>
                                    <span style={styles.orderAmount}>
                    ${order.totalAmount.toFixed(2)}
                  </span>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            </div>
        </Layout>
    );
}

function StatCard({
                      title, value, icon, color
                  }: {
    title: string;
    value: number | string;
    icon: string;
    color: string;
}) {
    return (
        <div style={cardStyles.card}>
            <div style={{
                ...cardStyles.icon,
                backgroundColor: color + '20'
            }}>
                <span style={{ fontSize: '24px' }}>{icon}</span>
            </div>
            <div>
                <div style={cardStyles.value}>{value}</div>
                <div style={cardStyles.title}>{title}</div>
            </div>
        </div>
    );
}

function getStatusColor(status: string): string {
    const colors: Record<string, string> = {
        Pending: '#fef3c7',
        Confirmed: '#dbeafe',
        Processing: '#e0e7ff',
        Shipped: '#d1fae5',
        Delivered: '#d1fae5',
        Cancelled: '#fee2e2'
    };
    return colors[status] ?? '#f3f4f6';
}

const styles: Record<string, React.CSSProperties> = {
    container: { padding: '32px' },
    header: { marginBottom: '32px' },
    title: {
        fontSize: '24px',
        fontWeight: '700',
        color: '#1a1a2e',
        margin: '0 0 4px'
    },
    subtitle: { color: '#666', margin: 0, fontSize: '14px' },
    statsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
        gap: '16px',
        marginBottom: '32px'
    },
    section: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)'
    },
    sectionTitle: {
        fontSize: '16px',
        fontWeight: '600',
        color: '#1a1a2e',
        margin: '0 0 16px'
    },
    empty: { color: '#999', fontSize: '14px' },
    orderList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px'
    },
    orderRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        padding: '12px',
        backgroundColor: '#f8f9fa',
        borderRadius: '8px'
    },
    orderId: {
        fontFamily: 'monospace',
        fontSize: '13px',
        color: '#666',
        flex: 1
    },
    statusBadge: {
        padding: '4px 10px',
        borderRadius: '100px',
        fontSize: '12px',
        fontWeight: '500',
        color: '#333'
    },
    orderAmount: {
        fontWeight: '600',
        color: '#1a1a2e',
        fontSize: '14px'
    }
};

const cardStyles: Record<string, React.CSSProperties> = {
    card: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '20px',
        display: 'flex',
        alignItems: 'center',
        gap: '16px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)'
    },
    icon: {
        width: '48px',
        height: '48px',
        borderRadius: '12px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center'
    },
    value: {
        fontSize: '24px',
        fontWeight: '700',
        color: '#1a1a2e'
    },
    title: { fontSize: '13px', color: '#666', marginTop: '2px' }
};