FROM node:20-alpine AS build

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Accept build arguments
ARG VITE_AUTH_API=http://localhost:5001
ARG VITE_GRAPHQL_API=http://localhost:5002/graphql
ARG VITE_WS_API=ws://localhost:5002/graphql

# Set as environment variables for Vite build
ENV VITE_AUTH_API=$VITE_AUTH_API
ENV VITE_GRAPHQL_API=$VITE_GRAPHQL_API
ENV VITE_WS_API=$VITE_WS_API

RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]