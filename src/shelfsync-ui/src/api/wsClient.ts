import { createClient } from 'graphql-ws';

const createWsClient = () => createClient({
    url: import.meta.env.VITE_WS_API
        ?? 'ws://localhost:5002/graphql',
    connectionParams: () => {
        const token = localStorage.getItem('accessToken');
        console.log('WS connecting with token:',
            token ? 'present' : 'MISSING');

        return token
            ? { Authorization: `Bearer ${token}` }
            : {};
    },

    retryAttempts: 5,

    retryWait: async () => {
        await new Promise(
            resolve => setTimeout(resolve, 3000)
        );
    },

    on: {
        connected: () =>
            console.log('WebSocket connected ✅'),
        closed: () =>
            console.log('WebSocket closed'),
        error: (err) =>
            console.error('WebSocket error:', err)
    }
});

export default createWsClient;