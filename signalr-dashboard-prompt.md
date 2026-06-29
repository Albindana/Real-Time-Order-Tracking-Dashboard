# Real-Time Order Tracking Dashboard — .NET SignalR Project Brief

> Use this file as a prompt for Claude Code. Paste it or reference it at the start of your session.

---

## Project Overview

Build a **Real-Time Order Tracking Dashboard** using **ASP.NET Core 8**, **SignalR**, **Redis**, and **React**. The dashboard displays live order activity — new orders appearing instantly, status changes broadcasting in real time, and live counters for revenue and active users.

**Goal:** A full-stack application demonstrating real-time push communication via WebSockets (SignalR), background processing with `IHostedService`, Redis pub/sub, and a React frontend that updates live without polling.

**GitHub repo name:** `RealTime-Order-Dashboard`

---

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| Real-time | SignalR |
| Background processing | `BackgroundService` (`IHostedService`) |
| Cache + Pub/Sub | Redis (StackExchange.Redis) |
| ORM | Entity Framework Core 8 |
| Database | SQLite (dev) / SQL Server (prod) — auto-detected |
| Authentication | ASP.NET Core Identity + JWT Bearer |
| Validation | FluentValidation |
| Mapping | Mapperly |
| Frontend | React 18 + TypeScript + Vite |
| Real-time client | @microsoft/signalr npm package |
| UI | Tailwind CSS |
| Docs | Swagger / Scalar |
| Orchestration | Docker + Docker Compose |
| Testing | xUnit + Moq |

---

## Solution Structure

```
RealTimeOrderDashboard.sln
├── src/
│   ├── RealTimeDashboard.API/           # ASP.NET Core API, SignalR hubs, controllers
│   ├── RealTimeDashboard.Application/   # Services, interfaces, DTOs, handlers
│   ├── RealTimeDashboard.Domain/        # Entities, enums, domain logic
│   └── RealTimeDashboard.Infrastructure/# EF Core, repositories, Redis, background services
│
├── frontend/                            # React + TypeScript + Vite
│   ├── src/
│   │   ├── components/
│   │   │   ├── Dashboard/
│   │   │   ├── OrderList/
│   │   │   └── StatsCards/
│   │   ├── hooks/
│   │   │   └── useSignalR.ts
│   │   ├── types/
│   │   └── App.tsx
│   ├── package.json
│   └── vite.config.ts
│
├── docker-compose.yml
├── docker-compose.override.yml
└── README.md
```

---

## Domain Entities

### AppUser (extends IdentityUser)
```
Id (string)
FirstName (string)
LastName (string)
CreatedAt (DateTime)
RefreshToken (string?)
RefreshTokenExpiry (DateTime?)
```

### Product
```
Id (Guid)
Name (string)
Price (decimal)
StockQuantity (int)
Category (string)
IsActive (bool)
CreatedAt (DateTime)
```

### Order
```
Id (Guid)
OrderNumber (string)          // e.g. "ORD-2024-00001"
CustomerId (string)
CustomerName (string)         // denormalized snapshot
CustomerEmail (string)        // denormalized snapshot
Status (enum: Pending, Processing, Shipped, Delivered, Cancelled)
TotalAmount (decimal)
ItemCount (int)
CreatedAt (DateTime)
UpdatedAt (DateTime)
Items (ICollection<OrderItem>)
```

### OrderItem
```
Id (Guid)
OrderId (Guid)
ProductId (Guid)
ProductName (string)          // snapshot
Quantity (int)
UnitPrice (decimal)           // snapshot
```

### DashboardStats (not persisted — Redis only)
```
TotalOrdersToday (int)
RevenueToday (decimal)
PendingOrders (int)
ActiveConnections (int)
RecentOrders (List<OrderSummaryDto>)  // last 10
```

---

## SignalR Hub

```csharp
// API/Hubs/OrderHub.cs
[Authorize]
public class OrderHub : Hub
{
    private readonly IDashboardStatsService _statsService;

    public OrderHub(IDashboardStatsService statsService)
    {
        _statsService = statsService;
    }

    // Called when a dashboard client connects — send current snapshot immediately
    public override async Task OnConnectedAsync()
    {
        var stats = await _statsService.GetCurrentStatsAsync();
        await Clients.Caller.SendAsync("InitialStats", stats);
        await base.OnConnectedAsync();
    }

    // Track active connection count in Redis
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _statsService.DecrementActiveConnectionsAsync();
        await base.OnDisconnectedAsync(exception);
    }
}
```

### Client-callable Hub methods (none — hub is server-push only)
The hub never receives calls from clients. It only pushes. Clients connect and listen.

### Server-to-client events (what clients subscribe to)

| Event name | Payload | Trigger |
|---|---|---|
| `InitialStats` | `DashboardStatsDto` | On connect |
| `NewOrderPlaced` | `OrderSummaryDto` | Order created |
| `OrderStatusChanged` | `OrderStatusUpdateDto` | Status updated |
| `StatsUpdated` | `DashboardStatsDto` | Any order change |
| `LowStockAlert` | `LowStockAlertDto` | Stock drops below 5 |

---

## Redis Architecture

Redis serves two roles:

### 1. Stats Cache
Store current dashboard stats so new connections get instant data:
```csharp
// Key: "dashboard:stats"
// Value: JSON-serialized DashboardStatsDto
// TTL: none (updated on every order change)

await _redis.StringSetAsync(
    "dashboard:stats",
    JsonSerializer.Serialize(stats)
);
```

### 2. Pub/Sub Channel
API publishes events, BackgroundService subscribes and broadcasts via SignalR:
```csharp
// Publisher (called from order service after saving to DB)
await _subscriber.PublishAsync("order-events", JsonSerializer.Serialize(new OrderEvent
{
    EventType = "OrderPlaced",
    OrderId = order.Id,
    Payload = JsonSerializer.Serialize(orderSummaryDto)
}));

// Subscriber (BackgroundService)
await _subscriber.SubscribeAsync("order-events", async (channel, message) =>
{
    var evt = JsonSerializer.Deserialize<OrderEvent>(message);
    // broadcast to SignalR clients based on EventType
});
```

---

## Background Service

```csharp
// Infrastructure/BackgroundServices/OrderEventWorker.cs
public class OrderEventWorker : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<OrderEventWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync("order-events", async (channel, message) =>
        {
            try
            {
                var evt = JsonSerializer.Deserialize<OrderEvent>(message!);

                switch (evt!.EventType)
                {
                    case "OrderPlaced":
                        var newOrder = JsonSerializer.Deserialize<OrderSummaryDto>(evt.Payload);
                        await _hubContext.Clients.All.SendAsync("NewOrderPlaced", newOrder, stoppingToken);
                        await _hubContext.Clients.All.SendAsync("StatsUpdated",
                            await GetUpdatedStats(), stoppingToken);
                        break;

                    case "OrderStatusChanged":
                        var update = JsonSerializer.Deserialize<OrderStatusUpdateDto>(evt.Payload);
                        await _hubContext.Clients.All.SendAsync("OrderStatusChanged", update, stoppingToken);
                        await _hubContext.Clients.All.SendAsync("StatsUpdated",
                            await GetUpdatedStats(), stoppingToken);
                        break;

                    case "LowStock":
                        var alert = JsonSerializer.Deserialize<LowStockAlertDto>(evt.Payload);
                        await _hubContext.Clients.All.SendAsync("LowStockAlert", alert, stoppingToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order event");
            }
        });

        // Keep alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

---

## API Endpoints

### Auth (`/api/auth`)
```
POST  /api/auth/register
POST  /api/auth/login         →  { accessToken, refreshToken }
POST  /api/auth/refresh
```

### Orders (`/api/orders`)
```
GET   /api/orders             →  paginated order list [Admin]
GET   /api/orders/{id}        →  order detail
POST  /api/orders             →  place new order (triggers SignalR broadcast)
PUT   /api/orders/{id}/status →  update status [Admin] (triggers SignalR broadcast)
GET   /api/orders/my          →  current user's orders
```

### Products (`/api/products`)
```
GET   /api/products           →  paginated product list
GET   /api/products/{id}      →  product detail
POST  /api/products           →  create [Admin]
PUT   /api/products/{id}      →  update [Admin]
```

### Dashboard (`/api/dashboard`)
```
GET   /api/dashboard/stats    →  current stats snapshot from Redis
GET   /api/dashboard/recent   →  last 20 orders
```

### SignalR Hub
```
WebSocket: /hubs/orders       →  connect here for real-time updates
```

---

## DTOs

```csharp
public record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    OrderStatus Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt
);

public record OrderStatusUpdateDto(
    Guid OrderId,
    string OrderNumber,
    OrderStatus OldStatus,
    OrderStatus NewStatus,
    DateTime UpdatedAt
);

public record DashboardStatsDto(
    int TotalOrdersToday,
    decimal RevenueToday,
    int PendingOrders,
    int ActiveConnections,
    List<OrderSummaryDto> RecentOrders
);

public record LowStockAlertDto(
    Guid ProductId,
    string ProductName,
    int CurrentStock
);
```

---

## Program.cs Configuration

```csharp
// SignalR
builder.Services.AddSignalR();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));

// Background worker
builder.Services.AddHostedService<OrderEventWorker>();

// CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")  // Vite dev server
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for SignalR
    });
});

// Map SignalR hub
app.MapHub<OrderHub>("/hubs/orders");
```

---

## React Frontend

### Project setup
```bash
cd frontend
npm create vite@latest . -- --template react-ts
npm install @microsoft/signalr
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

### useSignalR hook
```typescript
// src/hooks/useSignalR.ts
import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';

export function useSignalR(hubUrl: string, token: string) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = connection;

    connection.start()
      .then(() => setIsConnected(true))
      .catch(err => console.error('SignalR connection failed:', err));

    return () => {
      connection.stop();
    };
  }, [hubUrl, token]);

  return { connection: connectionRef.current, isConnected };
}
```

### Dashboard component structure
```typescript
// App.tsx — wire up all SignalR listeners
useEffect(() => {
  if (!connection) return;

  connection.on('InitialStats', (stats: DashboardStats) => {
    setStats(stats);
    setRecentOrders(stats.recentOrders);
  });

  connection.on('NewOrderPlaced', (order: OrderSummary) => {
    setRecentOrders(prev => [order, ...prev].slice(0, 20));
  });

  connection.on('OrderStatusChanged', (update: OrderStatusUpdate) => {
    setRecentOrders(prev =>
      prev.map(o => o.id === update.orderId
        ? { ...o, status: update.newStatus }
        : o
      )
    );
  });

  connection.on('StatsUpdated', (stats: DashboardStats) => {
    setStats(stats);
  });

  connection.on('LowStockAlert', (alert: LowStockAlert) => {
    // Show toast notification
  });
}, [connection]);
```

### UI Components to build
- `StatsCards` — 4 cards: Total Orders Today, Revenue Today, Pending Orders, Active Connections
- `OrderList` — scrollable table of recent orders with live status badges
- `StatusBadge` — colored badge per status (Pending=yellow, Processing=blue, Shipped=purple, Delivered=green, Cancelled=red)
- `ConnectionIndicator` — green dot when SignalR connected, red when disconnected
- `LowStockToast` — toast notification when LowStockAlert event fires

---

## Docker Compose

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    container_name: redis
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

  api:
    build:
      context: ./src
      dockerfile: RealTimeDashboard.API/Dockerfile
    container_name: dashboard-api
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Data Source=/data/dashboard.db
      - Redis__ConnectionString=redis:6379
      - JwtSettings__SecretKey=your-super-secret-key-min-32-chars
      - JwtSettings__Issuer=RealTimeDashboard
      - JwtSettings__Audience=DashboardClient
    depends_on:
      redis:
        condition: service_healthy
    volumes:
      - api-data:/data

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: dashboard-frontend
    ports:
      - "5173:80"
    depends_on:
      - api

volumes:
  api-data:
```

### API Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RealTimeDashboard.API/RealTimeDashboard.API.csproj", "RealTimeDashboard.API/"]
COPY ["RealTimeDashboard.Application/RealTimeDashboard.Application.csproj", "RealTimeDashboard.Application/"]
COPY ["RealTimeDashboard.Domain/RealTimeDashboard.Domain.csproj", "RealTimeDashboard.Domain/"]
COPY ["RealTimeDashboard.Infrastructure/RealTimeDashboard.Infrastructure.csproj", "RealTimeDashboard.Infrastructure/"]
RUN dotnet restore "RealTimeDashboard.API/RealTimeDashboard.API.csproj"
COPY . .
RUN dotnet publish "RealTimeDashboard.API/RealTimeDashboard.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RealTimeDashboard.API.dll"]
```

### Frontend Dockerfile
```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### nginx.conf (needed for React Router + API proxy)
```nginx
server {
    listen 80;

    location / {
        root /usr/share/nginx/html;
        index index.html;
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://api:8080;
    }

    location /hubs {
        proxy_pass http://api:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

---

## Seed Data

Seed on startup:
- Admin user: `admin@dashboard.com` / `Admin123!`
- Customer user: `customer@dashboard.com` / `Customer123!`
- 10 products across 3 categories with varying stock levels (2 products with stock = 3 to trigger low stock alerts)
- 15 orders in mixed statuses spread across the last 7 days

---

## Tests to Write

### Unit Tests
1. `OrderServiceTests`
   - PlaceOrder saves order and publishes Redis event
   - PlaceOrder throws when product out of stock
   - UpdateStatus publishes correct Redis event
   - UpdateStatus throws NotFoundException when order not found

2. `DashboardStatsServiceTests`
   - GetCurrentStats returns cached value from Redis when available
   - GetCurrentStats queries database when Redis cache is empty
   - Stats are updated correctly after new order

3. `OrderEventWorkerTests`
   - NewOrderPlaced event triggers correct SignalR broadcast
   - OrderStatusChanged event triggers correct SignalR broadcast
   - Malformed message is logged and does not crash the worker

### Integration Tests
1. `POST /api/orders` returns 201 and order appears in `/api/dashboard/recent`
2. `PUT /api/orders/{id}/status` updates status correctly
3. `GET /api/dashboard/stats` returns correct today counts

---

## Project Setup Instructions for Claude Code

Build in this order:

1. Create solution and 4 backend projects, add to `.sln`
2. Implement Domain entities (zero dependencies)
3. Set up EF Core + Identity + Redis in Infrastructure
4. Implement Application services and interfaces
5. Set up SignalR hub in API
6. Implement `OrderEventWorker` background service
7. Wire up all controllers
8. Add FluentValidation and global exception middleware
9. Add DB seeder
10. Scaffold React frontend with Vite + TypeScript
11. Implement `useSignalR` hook
12. Build UI components (StatsCards, OrderList, StatusBadge, ConnectionIndicator)
13. Wire up all SignalR event listeners in App.tsx
14. Write `docker-compose.yml` and all Dockerfiles
15. Write unit tests
16. Verify end-to-end: `docker compose up` → open dashboard → place order via Swagger → watch dashboard update live

---

## README to Generate

Generate a `README.md` with these exact sections:

### Header
Project name, one-line description, and a note that this demonstrates real-time push architecture with SignalR.

### Architecture Diagram
ASCII diagram showing: React client → ASP.NET Core API → SignalR Hub, and separately: Order Controller → Redis Pub/Sub → BackgroundService → SignalR Hub → React client.

### Tech Stack Table
Full table matching the one at the top of this brief.

### How It Works
3-paragraph explanation:
- How SignalR establishes a persistent WebSocket connection
- How placing an order triggers the Redis pub/sub → background service → SignalR broadcast chain
- Why Redis is used instead of calling SignalR directly from the controller

### Quick Start
```
Prerequisites: .NET 8 SDK, Node.js 20, Docker Desktop

# Start Redis
docker compose up redis -d

# Run API
dotnet run --project src/RealTimeDashboard.API

# Run frontend
cd frontend && npm install && npm run dev

# Or run everything
docker compose up
```

### Seeded accounts table
| Account | Password | Role |
|---|---|---|
| admin@dashboard.com | Admin123! | Admin |
| customer@dashboard.com | Customer123! | Customer |

### Design Decisions section
Explain:
- **Why SignalR over polling:** polling every second for 100 concurrent users = 100 req/s of wasted load. SignalR holds one persistent connection per client and pushes only when something changes.
- **Why Redis pub/sub between controller and background service:** the SignalR `IHubContext` could be called directly from the controller, but routing through Redis decouples the broadcast from the HTTP request lifecycle. It also means a second API instance would still broadcast correctly — the groundwork for horizontal scaling.
- **Why BackgroundService:** `IHostedService` runs independently of the HTTP pipeline, making it the right host for long-running subscribers that shouldn't block request processing.

### Test Coverage
List what the tests cover.

### API Reference
Swagger available at `https://localhost:{port}/swagger`

---

## Git Workflow

After every completed milestone, commit and push:

```bash
git add . && git commit -m "your message" && git push
```

### Recommended commit points

| Milestone | Commit message |
|---|---|
| Solution scaffolded, domain entities done | `feat: scaffold solution and domain entities` |
| EF Core + Redis + Identity wired | `feat: add infrastructure layer with EF Core and Redis` |
| SignalR hub implemented | `feat: add OrderHub with SignalR` |
| BackgroundService implemented | `feat: add OrderEventWorker background service` |
| All controllers done | `feat: add API controllers and endpoints` |
| Seeder working, API runs | `feat: add DB seeder and verify API startup` |
| React frontend scaffolded | `feat: scaffold React frontend with Vite and TypeScript` |
| useSignalR hook done | `feat: add useSignalR hook` |
| UI components done | `feat: add dashboard UI components` |
| SignalR listeners wired in App.tsx | `feat: wire up SignalR event listeners in frontend` |
| Docker Compose working | `feat: add Docker Compose and Dockerfiles` |
| Tests passing | `test: add unit and integration tests` |
| README written | `docs: add README with architecture and design decisions` |
| Final cleanup | `chore: final cleanup and polish` |

### Commit message format
```
feat: add X          # new feature
fix: resolve X       # bug fix
test: add X tests    # tests
docs: update README  # documentation
chore: X             # cleanup, config
refactor: X          # restructure without behavior change
```
