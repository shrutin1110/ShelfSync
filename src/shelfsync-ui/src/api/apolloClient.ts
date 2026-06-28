import { GraphQLClient } from 'graphql-request';

const getClient = () => new GraphQLClient(
    import.meta.env.VITE_GRAPHQL_API
    ?? 'http://localhost:5002/graphql',
    {
        headers: {
            authorization: localStorage.getItem('accessToken')
                ? `Bearer ${localStorage.getItem('accessToken')}`
                : '',
        },
    }
);

export default getClient;