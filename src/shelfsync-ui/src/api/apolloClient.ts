import { GraphQLClient } from 'graphql-request';

// Creates a GraphQL client that automatically
// adds the JWT token to every request
const getClient = () => new GraphQLClient(
    'http://localhost:5002/graphql',
    {
        headers: {
            authorization: localStorage.getItem('accessToken')
                ? `Bearer ${localStorage.getItem('accessToken')}`
                : '',
        },
    }
);

export default getClient;