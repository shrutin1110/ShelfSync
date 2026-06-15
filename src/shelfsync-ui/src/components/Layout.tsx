import { type ReactNode } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

interface LayoutProps {
    children: ReactNode;
}

export default function Layout({ children }: LayoutProps) {
    const { user, logout } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();

    const handleLogout = () => {
        logout();
        navigate('/login');
    };

    const isActive = (path: string) =>
        location.pathname === path;

    return (
        <div style={styles.container}>

            {/* Sidebar */}
            <aside style={styles.sidebar}>

                {/* Brand */}
                <div style={styles.brand}>
                    <span style={styles.brandIcon}>📦</span>
                    <span style={styles.brandName}>ShelfSync</span>
                </div>

                {/* Tenant name */}
                <div style={styles.tenantBadge}>
                    {user?.tenantName}
                </div>

                {/* Navigation links */}
                <nav style={styles.nav}>
                    <Link
                        to="/dashboard"
                        style={{
                            ...styles.navLink,
                            ...(isActive('/dashboard')
                                ? styles.navLinkActive
                                : {})
                        }}
                    >
                        🏠 Dashboard
                    </Link>

                    <Link
                        to="/products"
                        style={{
                            ...styles.navLink,
                            ...(isActive('/products')
                                ? styles.navLinkActive
                                : {})
                        }}
                    >
                        📦 Products
                    </Link>

                    <Link
                        to="/orders"
                        style={{
                            ...styles.navLink,
                            ...(isActive('/orders')
                                ? styles.navLinkActive
                                : {})
                        }}
                    >
                        🛒 Orders
                    </Link>
                </nav>

                {/* User info and logout */}
                <div style={styles.userSection}>
                    <div style={styles.userEmail}>{user?.email}</div>
                    <div style={styles.userRole}>{user?.role}</div>
                    <button
                        onClick={handleLogout}
                        style={styles.logoutButton}
                    >
                        Sign Out
                    </button>
                </div>
            </aside>

            {/* Main content */}
            <main style={styles.main}>
                {children}
            </main>
        </div>
    );
}

const styles: Record<string, React.CSSProperties> = {
    container: {
        display: 'flex',
        minHeight: '100vh',
        backgroundColor: '#f8f9fa'
    },
    sidebar: {
        width: '240px',
        backgroundColor: '#1a1a2e',
        color: 'white',
        display: 'flex',
        flexDirection: 'column',
        padding: '24px 0',
        flexShrink: 0
    },
    brand: {
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
        padding: '0 24px 24px',
        borderBottom: '1px solid rgba(255,255,255,0.1)'
    },
    brandIcon: {
        fontSize: '24px'
    },
    brandName: {
        fontSize: '18px',
        fontWeight: '700',
        color: 'white'
    },
    tenantBadge: {
        margin: '16px 24px',
        padding: '8px 12px',
        backgroundColor: 'rgba(79,70,229,0.3)',
        borderRadius: '6px',
        fontSize: '13px',
        color: '#a5b4fc',
        fontWeight: '500'
    },
    nav: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
        padding: '8px 12px',
        flex: 1
    },
    navLink: {
        display: 'block',
        padding: '10px 12px',
        borderRadius: '8px',
        color: 'rgba(255,255,255,0.7)',
        textDecoration: 'none',
        fontSize: '14px',
        fontWeight: '500',
        transition: 'all 0.15s'
    },
    navLinkActive: {
        backgroundColor: '#4f46e5',
        color: 'white'
    },
    userSection: {
        padding: '16px 24px',
        borderTop: '1px solid rgba(255,255,255,0.1)'
    },
    userEmail: {
        fontSize: '13px',
        color: 'rgba(255,255,255,0.7)',
        marginBottom: '2px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap'
    },
    userRole: {
        fontSize: '11px',
        color: '#a5b4fc',
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        marginBottom: '12px'
    },
    logoutButton: {
        width: '100%',
        padding: '8px',
        backgroundColor: 'rgba(255,255,255,0.1)',
        color: 'white',
        border: 'none',
        borderRadius: '6px',
        fontSize: '13px',
        cursor: 'pointer'
    },
    main: {
        flex: 1,
        overflow: 'auto'
    }
};