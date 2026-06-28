import { useState, useEffect, useRef } from 'react';
import createWsClient from '../api/wsClient';

interface UseSubscriptionOptions<T> {
    // The GraphQL subscription query string
    query: string;

    // Variables for the subscription
    variables?: Record<string, unknown>;

    // Called every time the server pushes new data
    onData: (data: T) => void;

    // Called if subscription errors out
    onError?: (error: unknown) => void;
}

// useSubscription hook
// Opens a WebSocket subscription and calls onData
// every time the server pushes an update
// Automatically cleans up when component unmounts
export function useSubscription<T>({
                                       query,
                                       variables,
                                       onData,
                                       onError
                                   }: UseSubscriptionOptions<T>) {
    const [isConnected, setIsConnected] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // useRef keeps a stable reference across renders
    // without causing re-renders when it changes
    const clientRef = useRef(createWsClient());

    useEffect(() => {
        const token = localStorage.getItem('accessToken');

        if (!token) {
            console.log('No token — skipping WebSocket connection');
            return;
        }

        const client = createWsClient();
        clientRef.current = client;

        const unsubscribe = client.subscribe(
            { query, variables },
            {
                next: (result) => {
                    if (result.data) {
                        setIsConnected(true);
                        setError(null);
                        onData(result.data as T);
                    }
                },
                error: (err) => {
                    console.error('Subscription error:', err);
                    setIsConnected(false);
                    setError('Subscription connection failed.');
                    onError?.(err);
                },
                complete: () => {
                    setIsConnected(false);
                }
            }
        );

        return () => {
            unsubscribe();
            client.dispose();
        };
    }, [query]);

    return { isConnected, error };
}