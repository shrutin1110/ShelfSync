import {
    createContext,
    useContext,
    useState,
    useEffect,
    type ReactNode
} from 'react';
import type { User } from '../types';
import { jwtDecode } from 'jwt-decode';

// Shape of the auth context
interface AuthContextType {
    user: User | null;
    accessToken: string | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    login: (accessToken: string, refreshToken: string) => void;
    logout: () => void;
}

// Create the context
const AuthContext = createContext<AuthContextType | null>(null);

// JWT payload shape
interface JwtPayload {
    // .NET uses full URI claim names
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': string;
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress': string;
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': string;
    tenantId: string;
    tenantName: string;
    plan: string;
    exp: number;
}

// AuthProvider wraps your entire app
// Any component inside can access the auth state
export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<User | null>(null);
    const [accessToken, setAccessToken] = useState<string | null>(null);
    const [isLoading, setIsLoading] = useState(true);

    // On app load check if the token exists in localStorage
    // and is not expired
    useEffect(() => {
        const token = localStorage.getItem('accessToken');

        if (token) {
            try {
                const decoded = jwtDecode<JwtPayload>(token);
                const isExpired = decoded.exp * 1000 < Date.now();

                if (!isExpired) {
                    setAccessToken(token);
                    setUser({
                        email: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'],
                        role: decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
                        tenantName: decoded.tenantName,
                        tenantId: decoded.tenantId
                    });
                } else {
                    // Token expired — clear storage
                    localStorage.removeItem('accessToken');
                    localStorage.removeItem('refreshToken');
                }
            } catch {
                // Invalid token — clear storage
                localStorage.removeItem('accessToken');
                localStorage.removeItem('refreshToken');
            }
        }

        setIsLoading(false);
    }, []);

    const login = (newAccessToken: string, newRefreshToken: string) => {
        // Store tokens in localStorage
        // Note: httpOnly cookies are more secure
        // but require server-side setup
        // localStorage is fine for development
        localStorage.setItem('accessToken', newAccessToken);
        localStorage.setItem('refreshToken', newRefreshToken);

        // Decode token to get user info
        const decoded = jwtDecode<JwtPayload>(newAccessToken);

        setAccessToken(newAccessToken);
        setUser({
            email: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'],
            role: decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
            tenantName: decoded.tenantName,
            tenantId: decoded.tenantId
        });
    };

    const logout = () => {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        setAccessToken(null);
        setUser(null);
    };

    return (
        <AuthContext.Provider value={{
            user,
            accessToken,
            isAuthenticated: !!user,
            isLoading,
            login,
            logout
        }}>
            {children}
        </AuthContext.Provider>
    );
}

// Custom hook to use auth context
// Usage: const { user, login, logout } = useAuth();
export function useAuth() {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used inside AuthProvider');
    }
    return context;
}