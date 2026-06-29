# Real-Time Order Tracking Dashboard

A full-stack dashboard that shows live order activity — new orders appear instantly, status changes broadcast in real time, and revenue/connection counters update without polling.

This project demonstrates **real-time push architecture**: an ASP.NET Core API pushes events to a React client over a persistent **SignalR** WebSocket connection, with **Redis pub/sub** decoupling the broadcast from the HTTP request that triggered it.

---

## Architecture Diagram

```
            Persistent WebSocket (SignalR)
  ┌─────────────┐   InitialStats / StatsUpdated   ┌──────────────────┐
  │   React     │◀════════════════════════════════│  ASP.NET Core    │
  │   client    │   NewOrderPlaced / LowStock      │  SignalR Hub     │
  └──────┬──────┘                                  └────────▲─────────┘
         │ REST (place order, login)                        │ broadcast
         ▼                                                   │
  ┌─────────────┐     publish      ┌───────────┐    subscribe (IHostedService)
  │   Orders    │─────────────────▶│  Redis    │───────────▶┌──────────────────┐
  │  Controller │  "order-events"  │  Pub/Sub  │            │ OrderEventWorker  │
  └─────────────┘                  └───────────┘            │ (BackgroundService)│
                                                            └─────────┬─────────┘
                                                                      │ IHubContext
                                                                      ▼
                                                            broadcast to all clients
```

The order controller never touches SignalR directly. It saves the order, publishes an event to Redis, and returns. A background worker — subscribed to that Redis channel — is what fans the event out to every connected dashboard.

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
| Real-time client | `@microsoft/signalr` npm package |
| UI | Tailwind CSS |
| Docs | Swagger |
| Orchestration | Docker + Docker Compose |
| Testing | xUnit + Moq |

---

## How It Works

**SignalR holds the connection open.** When the dashboard loads, the React client opens a single WebSocket to `/hubs/orders`, authenticating with its JWT (passed via the `access_token` query string, since the WebSocket handshake can't send an `Authorization` header). That one connection stays open for the life of the session. The server can push a message down it at any moment — there is no request/response round-trip and no polling. On connect, the hub immediately sends an `InitialStats` snapshot so the dashboard is populated instantly.

**Placing an order kicks off a pub/sub → background → broadcast chain.** A `POST /api/orders` saves the order through EF Core, then publishes a small JSON event to the Redis `order-events` channel and returns `201`. Meanwhile, the `OrderEventWorker` — a long-running `BackgroundService` subscribed to that channel — receives the event, refreshes the cached dashboard stats, and calls `IHubContext<OrderHub>` to broadcast `NewOrderPlaced` and `StatsUpdated` to every connected client. The browser's listeners fire and the UI re-renders. End to end this takes milliseconds and involves no client polling.

**Redis sits between the controller and the broadcast on purpose.** The controller *could* inject `IHubContext` and broadcast directly, but routing through Redis decouples the broadcast from the HTTP request lifecycle: the request completes the moment the event is published, and the slower fan-out happens off the request thread. It also unlocks horizontal scaling — if a second API instance places an order, *every* instance's worker still receives the event and broadcasts to its own connected clients. Redis additionally caches the current `dashboard:stats` snapshot so a freshly connected client gets data without hitting the database.

---

## Quick Start

```
Prerequisites: .NET 8 SDK, Node.js 20, Docker Desktop
```

### Run everything with Docker

```bash
docker compose up --build
# Dashboard: http://localhost:5173
# API + Swagger: http://localhost:5001/swagger
```

### Or run locally (hybrid)

```bash
# 1. Start Redis only
docker compose up redis -d

# 2. Run the API (http://localhost:5011, Swagger at /swagger)
dotnet run --project src/RealTimeDashboard.API

# 3. Run the frontend (http://localhost:5173)
cd frontend && npm install && npm run dev
```

The database (SQLite) is created and seeded automatically on first run.

### Local URLs

Once the stack is up, open these in your browser:

| Service | Docker (`docker compose up`) | Local (hybrid) |
|---|---|---|
| 🖥️ Dashboard (frontend) | http://localhost:5173 | http://localhost:5173 |
| 🔌 API base | http://localhost:5001 | http://localhost:5011 |
| 📖 Swagger UI | http://localhost:5001/swagger | http://localhost:5011/swagger |
| 🔁 SignalR hub | ws://localhost:5001/hubs/orders | ws://localhost:5011/hubs/orders |

> **Tip:** if `localhost:5173` shows the wrong app (e.g. another local dev server bound to the same port over IPv6), use **http://127.0.0.1:5173** instead.

---

## Seeded Accounts

| Account | Password | Role |
|---|---|---|
| admin@dashboard.com | Admin123! | Admin |
| customer@dashboard.com | Customer123! | Customer |

Admins can view all orders, update order status, and manage products. Customers can place orders and view their own.

---

## Design Decisions

- **Why SignalR over polling.** Polling every second for 100 concurrent users is 100 req/s of mostly-wasted load, and updates still lag by up to a second. SignalR holds one persistent connection per client and pushes only when something actually changes — lower latency and dramatically less load.

- **Why Redis pub/sub between the controller and the background service.** `IHubContext` could be called straight from the controller, but routing the broadcast through Redis decouples it from the HTTP request lifecycle, so the request returns as soon as the event is published. Just as importantly, it's the groundwork for horizontal scaling: with multiple API instances, an order placed on one instance still reaches clients connected to any instance, because every instance's worker is subscribed to the same channel.

- **Why a `BackgroundService`.** `IHostedService` runs independently of the HTTP pipeline, which makes it the right host for a long-running Redis subscriber that must stay alive for the life of the app and must not block request processing. The worker is also hardened to retry its subscription if Redis isn't ready yet (e.g. during `docker compose` startup) instead of crashing the host.

- **Hub lives in Infrastructure, not the API project.** Because the worker (Infrastructure) needs `IHubContext<OrderHub>`, placing the hub in the API would create a circular project reference. Keeping the hub in Infrastructure — which the API references — resolves this cleanly while the API still maps the route via `app.MapHub<OrderHub>("/hubs/orders")`.

---

## Test Coverage

Run with `dotnet test`.

**Unit tests**
- `OrderServiceTests` — placing an order saves it and publishes a Redis event; stock is decremented; a low-stock alert fires when stock drops below the threshold; ordering more than available throws; status updates publish the correct event with old/new status; updating a missing order throws `NotFoundException`.
- `OrderEventWorkerTests` — an `OrderPlaced` event broadcasts `NewOrderPlaced` + `StatsUpdated`; an `OrderStatusChanged` event broadcasts `OrderStatusChanged`; a malformed message is swallowed (logged) and never crashes the worker or broadcasts.

**Integration tests** (`WebApplicationFactory` with in-memory EF Core and a mocked Redis)
- `POST /api/orders` returns `201` and the order appears in `/api/dashboard/recent`.
- `PUT /api/orders/{id}/status` updates the order's status.
- `GET /api/dashboard/stats` reflects newly placed orders in today's counts.
- Requests without a token receive `401`.

---

## API Reference

Swagger UI is available in development at `http://localhost:5011/swagger` (local) or `http://localhost:5001/swagger` (Docker).

| Method | Route | Notes |
|---|---|---|
| POST | `/api/auth/register` | Register a customer |
| POST | `/api/auth/login` | Returns `{ accessToken, refreshToken }` |
| POST | `/api/auth/refresh` | Exchange a refresh token |
| GET | `/api/orders` | Paginated list — **Admin** |
| GET | `/api/orders/my` | Current user's orders |
| GET | `/api/orders/{id}` | Order detail |
| POST | `/api/orders` | Place an order → triggers SignalR broadcast |
| PUT | `/api/orders/{id}/status` | Update status — **Admin** → triggers broadcast |
| GET | `/api/products` | Paginated product list |
| POST/PUT | `/api/products` | Create/update — **Admin** |
| GET | `/api/dashboard/stats` | Current stats snapshot |
| GET | `/api/dashboard/recent` | Last 20 orders |
| WS | `/hubs/orders` | SignalR — connect for real-time updates |

---

## Project Structure

```
RealTimeOrderDashboard.sln
├── src/
│   ├── RealTimeDashboard.API/            # Controllers, Program.cs, middleware, Dockerfile
│   ├── RealTimeDashboard.Application/    # DTOs, interfaces, services, validators, Mapperly mappers
│   ├── RealTimeDashboard.Domain/         # Entities, enums, domain exceptions
│   └── RealTimeDashboard.Infrastructure/ # EF Core, Identity, Redis, SignalR hub, background worker, seeder
├── tests/RealTimeDashboard.Tests/        # xUnit unit + integration tests
├── frontend/                             # React + TypeScript + Vite + Tailwind
├── docker-compose.yml
└── docker-compose.override.yml
```
