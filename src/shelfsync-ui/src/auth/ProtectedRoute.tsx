import { Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

interface ProtectedRouteProps {
    children: React.ReactNode;
}

// Wraps any page that requires authentication
// If not logged in → redirect to /login
// If logged in → show the page
export function ProtectedRoute({ children }: ProtectedRouteProps) {
    const { isAuthenticated, isLoading } = useAuth();

    // While checking localStorage for existing token
    // show nothing to prevent flash of login page
    if (isLoading) {
        return (
            <div style={{
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center',
                height: '100vh',
                fontSize: '18px',
                color: '#666'
            }}>
                Loading...
            </div>
        );
    }

    // Not authenticated → redirect to login
    if (!isAuthenticated) {
        return <Navigate to="/login" replace />;
    }

    // Authenticated → show the requested page
    return <>{children}</>;
}