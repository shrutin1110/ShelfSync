import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../auth/AuthContext';
import type { AuthResponse } from '../types';

const AUTH_API = (import.meta.env.VITE_AUTH_API ?? 'http://localhost:5000') + '/api/auth';

export default function LoginPage() {
    const navigate = useNavigate();
    const { login } = useAuth();

    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [isRegister, setIsRegister] = useState(false);
    const [companyName, setCompanyName] = useState('');

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setIsLoading(true);

        try {
            let response;

            if (isRegister) {
                response = await axios.post<AuthResponse>(
                    `${AUTH_API}/register`,
                    { email, password, companyName }
                );
            } else {
                response = await axios.post<AuthResponse>(
                    `${AUTH_API}/login`,
                    { email, password }
                );
            }

            login(
                response.data.accessToken,
                response.data.refreshToken
            );

            navigate('/dashboard');
        } catch (err: any) {
            setError(
                err.response?.data?.message
                ?? 'Something went wrong. Please try again.'
            );
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div style={styles.container}>
            <div style={styles.card}>

                <div style={styles.header}>
                    <h1 style={styles.logo}>📦 ShelfSync</h1>
                    <p style={styles.subtitle}>
                        {isRegister
                            ? 'Create your business account'
                            : 'Sign in to your account'}
                    </p>
                </div>

                {error && (
                    <div style={styles.error}>
                        {error}
                    </div>
                )}

                <form onSubmit={handleSubmit} style={styles.form}>

                    {isRegister && (
                        <div style={styles.field}>
                            <label style={styles.label}>Company Name</label>
                            <input
                                type="text"
                                value={companyName}
                                onChange={e => setCompanyName(e.target.value)}
                                placeholder="Acme Clothing"
                                required={isRegister}
                                style={styles.input}
                            />
                        </div>
                    )}

                    <div style={styles.field}>
                        <label style={styles.label}>Email</label>
                        <input
                            type="email"
                            value={email}
                            onChange={e => setEmail(e.target.value)}
                            placeholder="you@company.com"
                            required
                            style={styles.input}
                        />
                    </div>

                    <div style={styles.field}>
                        <label style={styles.label}>Password</label>
                        <input
                            type="password"
                            value={password}
                            onChange={e => setPassword(e.target.value)}
                            placeholder="••••••••"
                            required
                            style={styles.input}
                        />
                    </div>

                    <button
                        type="submit"
                        disabled={isLoading}
                        style={{
                            ...styles.button,
                            opacity: isLoading ? 0.7 : 1
                        }}
                    >
                        {isLoading
                            ? 'Please wait...'
                            : isRegister ? 'Create Account' : 'Sign In'}
                    </button>
                </form>

                <p style={styles.toggle}>
                    {isRegister
                        ? 'Already have an account? '
                        : "Don't have an account? "}
                    <button
                        onClick={() => {
                            setIsRegister(!isRegister);
                            setError('');
                        }}
                        style={styles.toggleButton}
                    >
                        {isRegister ? 'Sign In' : 'Register'}
                    </button>
                </p>

                <p style={styles.storefrontLink}>
                    Want to place an order?{' '}
                    <a href="/storefront" style={styles.storefrontAnchor}>
                        Visit the Storefront
                    </a>
                </p>

            </div>
        </div>
    );
}

const styles: Record<string, React.CSSProperties> = {
    container: {
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: '#f5f5f5',
        padding: '20px'
    },
    card: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '40px',
        width: '100%',
        maxWidth: '420px',
        boxShadow: '0 4px 24px rgba(0,0,0,0.08)'
    },
    header: {
        textAlign: 'center',
        marginBottom: '32px'
    },
    logo: {
        fontSize: '28px',
        fontWeight: '700',
        color: '#1a1a2e',
        margin: '0 0 8px'
    },
    subtitle: {
        color: '#666',
        margin: 0,
        fontSize: '15px'
    },
    error: {
        backgroundColor: '#fff0f0',
        border: '1px solid #ffcccc',
        borderRadius: '8px',
        padding: '12px',
        color: '#cc0000',
        fontSize: '14px',
        marginBottom: '16px'
    },
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px'
    },
    field: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px'
    },
    label: {
        fontSize: '14px',
        fontWeight: '500',
        color: '#333'
    },
    input: {
        padding: '10px 14px',
        borderRadius: '8px',
        border: '1px solid #ddd',
        fontSize: '15px',
        outline: 'none',
        transition: 'border-color 0.2s'
    },
    button: {
        padding: '12px',
        backgroundColor: '#4f46e5',
        color: 'white',
        border: 'none',
        borderRadius: '8px',
        fontSize: '15px',
        fontWeight: '600',
        cursor: 'pointer',
        marginTop: '8px'
    },
    toggle: {
        textAlign: 'center',
        marginTop: '24px',
        fontSize: '14px',
        color: '#666'
    },
    toggleButton: {
        background: 'none',
        border: 'none',
        color: '#4f46e5',
        cursor: 'pointer',
        fontWeight: '600',
        fontSize: '14px'
    },
    storefrontLink: {
        textAlign: 'center',
        marginTop: '16px',
        fontSize: '13px',
        color: '#9ca3af'
    },
    storefrontAnchor: {
        color: '#4f46e5',
        fontWeight: '600',
        textDecoration: 'none'
    }
};
