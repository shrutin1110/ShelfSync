# ShelfSync

A multi-tenant SaaS order management and warehouse platform for e-commerce businesses. ShelfSync handles the operational backend — stock reservation, order fulfilment, invoice generation, and real-time warehouse management.

---

## What is ShelfSync?

ShelfSync is a B2B SaaS tool used by businesses to manage their entire order lifecycle. In production, orders flow in automatically from customer-facing storefronts via API integration. The admin dashboard provides real-time visibility into order pipeline, inventory levels, and revenue metrics.

```
Customer places order on storefront
        ↓
ShelfSync reserves stock via gRPC
        ↓
Order saved to PostgreSQL
        ↓
SQS events trigger notifications and invoice generation
        ↓
Lambda generates PDF invoice and uploads to S3
        ↓
Admin manages order through fulfilment pipeline
        ↓
Real-time status updates via WebSocket subscriptions
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend Services | .NET 10, C# |
| API | GraphQL (Hot Chocolate 16) |
| Service Communication | gRPC (Protobuf) |
| Database | PostgreSQL 16 (EF Core) |
| Cache | Redis |
| Message Queue | AWS SQS |
| File Storage | AWS S3 |
| Invoice Generation | AWS Lambda + QuestPDF |
| Frontend | React 19, TypeScript, Vite |
| Containerisation | Docker, Docker Compose |

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     React Frontend                       │
│              (TypeScript + Vite + graphql-ws)            │
└──────────────┬──────────────────────┬───────────────────┘
               │ GraphQL / WebSocket  │ REST
               ▼                      ▼
┌──────────────────────┐   ┌──────────────────────┐
│   Orders Service     │   │    Auth Service       │
│   Port 5002          │   │    Port 5001          │
│   GraphQL API        │◄──│    JWT + Tenants      │
│   Redis Cache        │   └──────────────────────┘
│   SQS Publisher      │
└──────┬───────────────┘
       │ gRPC
       ▼
┌──────────────────────┐
│  Warehouse Service   │
│  Port 5003           │
│  Stock Reservation   │
└──────────────────────┘
       │
       │ SQS Events
       ▼
┌──────────────────────┐   ┌──────────────────────┐
│ Notifications Service│   │  Lambda              │
│  Port 5004           │   │  Invoice Generator   │
│  SQS Consumer        │   │  QuestPDF + S3       │
└──────────────────────┘   └──────────────────────┘
```

---

## Project Structure

```
ShelfSync/
├── src/
│   ├── ShelfSync.Auth/              # JWT auth, tenant registration
│   ├── ShelfSync.Orders/            # GraphQL API, order management
│   ├── ShelfSync.Warehouse/         # gRPC stock reservation
│   ├── ShelfSync.Notifications/     # SQS consumer, notifications
│   ├── ShelfSync.Lambda.InvoiceGenerator/  # PDF invoice Lambda
│   ├── ShelfSync.Shared/            # Shared entities, interfaces
│   └── shelfsync-ui/                # React frontend
├── compose.yaml                     # Docker Compose
└── README.md
```

---

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- Docker Desktop
- AWS Account with:
  - IAM user with S3, SQS, Lambda permissions
  - S3 bucket: `shelfsync-dev-assets-tenants`
  - SQS queues (see AWS Setup below)
  - Lambda function: `ShelfSync-InvoiceGenerator`

---

## Local Development Setup

### 1. Clone the repository

```bash
git clone https://github.com/yourusername/ShelfSync.git
cd ShelfSync
```

### 2. Start infrastructure

```bash
# Start PostgreSQL and Redis
docker run -d \
  --name shelfsync-db \
  -e POSTGRES_DB=shelfsync \
  -e POSTGRES_USER=shelfsync \
  -e POSTGRES_PASSWORD=shelfsync123 \
  -p 5432:5432 \
  postgres:16

docker run -d \
  --name shelfsync-redis \
  -p 6379:6379 \
  redis:7-alpine
```

### 3. Run database migrations

```bash
# Auth migrations (creates Users, Tenants, RefreshTokens)
cd src/ShelfSync.Auth
dotnet ef database update

# Warehouse migrations (creates WarehouseLocations)
cd ../ShelfSync.Warehouse
dotnet ef database update
```

### 4. Configure secrets

```bash
# Auth service
cd src/ShelfSync.Auth
dotnet user-secrets set "JwtSettings:SecretKey" "shelfsync-super-secret-key-minimum-32-characters-long!!"
dotnet user-secrets set "JwtSettings:Issuer" "ShelfSync"
dotnet user-secrets set "JwtSettings:Audience" "ShelfSync-Client"

# Orders service
cd ../ShelfSync.Orders
dotnet user-secrets set "AWS:AccessKeyId" "YOUR_AWS_ACCESS_KEY"
dotnet user-secrets set "AWS:SecretAccessKey" "YOUR_AWS_SECRET_KEY"
dotnet user-secrets set "AWS:S3Region" "us-east-1"
dotnet user-secrets set "AWS:SqsRegion" "us-east-1"
dotnet user-secrets set "AWS:BucketName" "shelfsync-dev-assets-tenants"

# Notifications service
cd ../ShelfSync.Notifications
dotnet user-secrets set "AWS:AccessKeyId" "YOUR_AWS_ACCESS_KEY"
dotnet user-secrets set "AWS:SecretAccessKey" "YOUR_AWS_SECRET_KEY"
dotnet user-secrets set "AWS:Region" "us-east-1"
```

### 5. Configure frontend environment

```bash
cd src/shelfsync-ui
cp .env.example .env
```

Edit `.env`:

```
VITE_AUTH_API=http://localhost:5000
VITE_GRAPHQL_API=http://localhost:5002/graphql
VITE_WS_API=ws://localhost:5002/graphql
```

### 6. Start all services

Open 4 separate terminals:

```bash
# Terminal 1 — Warehouse (start first, Orders depends on it)
cd src/ShelfSync.Warehouse
dotnet run

# Terminal 2 — Auth
cd src/ShelfSync.Auth
dotnet run

# Terminal 3 — Orders
cd src/ShelfSync.Orders
dotnet run

# Terminal 4 — Notifications
cd src/ShelfSync.Notifications
dotnet run
```

### 7. Start frontend

```bash
cd src/shelfsync-ui
npm install
npm run dev
```

Open `http://localhost:5173`

---

## Running with Docker

### 1. Create .env file at project root

```bash
cat > .env << 'EOF'
AWS_ACCESS_KEY_ID=your_access_key_here
AWS_SECRET_ACCESS_KEY=your_secret_key_here
EOF
```

### 2. Start local infrastructure

```bash
docker start shelfsync-db shelfsync-redis
```

### 3. Build and run all containers

```bash
docker compose up --build
```

### 4. Access the application

- Frontend: `http://localhost:3000`
- Auth API: `http://localhost:5001`
- Orders GraphQL: `http://localhost:5002/graphql`
- Warehouse gRPC: `http://localhost:5003`

### Useful Docker commands

```bash
# Start all services
docker compose up

# Start in background
docker compose up -d

# Stop all services
docker compose down

# View logs for a service
docker compose logs auth
docker compose logs orders -f

# Rebuild after code changes
docker compose up --build

# Rebuild specific service only
docker compose up --build auth
docker compose up --build frontend
```

---

## AWS Setup

### SQS Queues (us-east-1)

Create the following queues in AWS SQS:

```
shelfsync-order-created
shelfsync-order-created-dlq
shelfsync-order-shipped
shelfsync-order-shipped-dlq
shelfsync-invoice-generate
shelfsync-invoice-generate-dlq
shelfsync-inventory-low
shelfsync-inventory-low-dlq
```

Set each main queue's Dead Letter Queue to its corresponding `-dlq` queue with `maxReceiveCount: 3`.

### S3 Bucket

```bash
aws s3 mb s3://shelfsync-dev-assets-tenants --region us-east-1
```

Add CORS configuration to allow browser uploads:

```json
[
  {
    "AllowedHeaders": ["*"],
    "AllowedMethods": ["GET", "PUT", "POST"],
    "AllowedOrigins": ["http://localhost:5173", "http://localhost:3000"],
    "ExposeHeaders": []
  }
]
```

### Lambda Deployment

```bash
cd src/ShelfSync.Lambda.InvoiceGenerator
dotnet lambda deploy-function ShelfSync-InvoiceGenerator
```

Set Lambda environment variable:
```
S3_BUCKET_NAME = shelfsync-dev-assets-tenants
```

Add SQS trigger: `shelfsync-invoice-generate` queue.

### IAM Permissions

Your IAM user needs these policies:
- `AmazonS3FullAccess` (or scoped to your bucket)
- `AmazonSQSFullAccess` (or scoped to your queues)
- `AWSLambda_FullAccess` (for deployment)

---



## Key Features

### Multi-Tenancy
Every database table has a `TenantId` column. All queries filter by the tenant extracted from the JWT token. Tenants are completely isolated — they cannot see each other's data even if they guess another tenant's IDs.

### Real-Time Updates
Order status changes and new orders are broadcast to all connected clients via GraphQL WebSocket subscriptions. Built on Hot Chocolate's in-memory pub/sub. No polling required.

### gRPC Stock Reservation
When an order is placed, the Orders service calls the Warehouse service synchronously via gRPC to reserve stock. If insufficient stock is available the order is rejected before saving. Binary protobuf encoding over HTTP/2.

### Event-Driven Architecture
After an order is confirmed, events are published to AWS SQS. The Notifications service consumes these asynchronously. If Notifications is down, messages wait in the queue and are processed when it recovers. Dead letter queues capture failed messages.

### Redis Caching
Products are cached in Redis using the cache-aside pattern with a 5-minute TTL. Cache is invalidated when products are added or updated. Reduces database load significantly under high traffic.

### Serverless Invoice Generation
PDF invoices are generated by an AWS Lambda function triggered by the SQS invoice queue. Lambda only runs when an order is placed — zero cost at idle. QuestPDF generates professional invoices with itemised breakdown stored in S3.

### Storefront Simulator
A public `/storefront` page demonstrates the customer order flow without requiring customer accounts. Selects a tenant, loads their products, allows cart-based ordering, and places orders via a scoped storefront token. Orders appear in the admin dashboard in real time.

---

## GraphQL API

Access Banana Cake Pop (GraphQL IDE) at `http://localhost:5002/graphql`

### Queries

```graphql
# Get all products for current tenant
query {
  products {
    id
    name
    sku
    price
    stockQuantity
    isActive
  }
}

# Get all orders with items
query {
  orders {
    id
    status
    totalAmount
    createdAt
    items {
      quantity
      unitPrice
      product {
        name
        sku
      }
    }
  }
}
```

### Mutations

```graphql
# Place a new order
mutation {
  placeOrder(input: {
    items: [
      { productId: "uuid-here", quantity: 2 }
    ]
    notes: "Please gift wrap"
  }) {
    success
    errorMessage
    orderId
  }
}

# Update order status
mutation {
  updateOrderStatus(input: {
    orderId: "uuid-here"
    newStatus: SHIPPED
  }) {
    id
    status
    updatedAt
  }
}

# Add a product
mutation {
  addProduct(input: {
    name: "Blue Jeans"
    sku: "JEANS-BLUE-M"
    price: 59.99
    initialStock: 50
    aisle: "B"
    shelf: "3"
  }) {
    id
    name
  }
}
```

### Subscriptions

```graphql
# Real-time order status updates
subscription {
  onOrderStatusChanged {
    id
    status
    updatedAt
  }
}

# Real-time new order notifications
subscription {
  onNewOrderPlaced {
    id
    status
    totalAmount
    createdAt
  }
}
```

---

## Auth Endpoints

```
POST /api/auth/register
  Body: { email, password, companyName }
  Creates new tenant + admin user

POST /api/auth/login
  Body: { email, password }
  Returns: { accessToken, refreshToken, email, role, tenantName }

GET  /api/auth/tenants
  Public — returns all registered tenants
  Used by storefront simulator

POST /api/auth/storefront-token
  Body: { tenantId }
  Returns scoped token for storefront order placement
  Creates a service account user for the tenant if not exists
```

---

## Environment Variables Reference

### Auth Service
```
ConnectionStrings__DefaultConnection   PostgreSQL connection string
JwtSettings__SecretKey                 JWT signing key (min 32 chars)
JwtSettings__Issuer                    ShelfSync
JwtSettings__Audience                  ShelfSync-Client
JwtSettings__AccessTokenExpiryMinutes  15
JwtSettings__RefreshTokenExpiryDays    7
```

### Orders Service
```
ConnectionStrings__DefaultConnection   PostgreSQL connection string
ConnectionStrings__Redis               Redis connection string
JwtSettings__SecretKey                 Same as Auth
GrpcSettings__WarehouseServiceUrl      http://warehouse:5003
AWS__AccessKeyId                       AWS credentials
AWS__SecretAccessKey                   AWS credentials
AWS__S3Region                          us-east-1
AWS__SqsRegion                         us-east-1
AWS__BucketName                        shelfsync-dev-assets-tenants
SQS__OrderCreatedQueueUrl              Full SQS queue URL
SQS__OrderShippedQueueUrl              Full SQS queue URL
SQS__InvoiceGenerateQueueUrl           Full SQS queue URL
SQS__InventoryLowQueueUrl              Full SQS queue URL
```

### Notifications Service
```
AWS__AccessKeyId                       AWS credentials
AWS__SecretAccessKey                   AWS credentials
AWS__Region                            us-east-1
SQS__OrderCreatedQueueUrl              Full SQS queue URL
SQS__OrderShippedQueueUrl              Full SQS queue URL
```

### Lambda
```
S3_BUCKET_NAME                         shelfsync-dev-assets-tenants
```

## License

MIT
