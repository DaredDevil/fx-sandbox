# FX Sandbox — Paper Trading Terminal

Full-stack foreign-exchange paper trading simulator. USD 10,000 starting capital, live rate feed, automatic limit-order filling. Pairs: USD/EUR · USD/GBP · USD/CHF.

---

## Architecture

**Pattern:** Vertical Slice Architecture — each HTTP feature is a self-contained static class under `Features/`. Cross-cutting infrastructure lives in `Services/`.

```
src/FxSandbox.Api/
├── Features/
│   ├── Orders/          OrdersEndpoints  — GET · POST · DELETE /api/orders
│   ├── Rates/           RatesEndpoints   — GET /api/rates
│   ├── Positions/       PositionsEndpoints — GET /api/positions
│   └── Account/         AccountEndpoints — GET /api/account
├── Domain/              LimitOrder, Position, OrderSide, OrderStatus
└── Services/
    ├── TradingEngine         ITradingEngine — all in-memory state
    ├── OrderMatchingService  BackgroundService — fills pending orders every 500 ms
    ├── RateSimulatorService  BackgroundService — random-walk rates every 500 ms
    └── Locking/
        ├── ILockProvider     abstraction (swap LocalLockProvider → Redis for multi-pod)
        └── LocalLockProvider ReaderWriterLockSlim — concurrent reads, exclusive writes

fx-sandbox-ui/           React 18 + Vite + TailwindCSS
tests/
├── UnitTests            TradingEngine domain logic, concurrency invariants (43 tests)
├── IntegrationTests     Engine + matching service wired together (5 tests)
├── ApiTests             HTTP contract, status codes, validation (14 tests)
└── FunctionalTests      End-to-end trader scenarios via WebApplicationFactory (7 tests)
```

**Thread-safety:** `ILockProvider` wraps `ReaderWriterLockSlim`. All GET paths hold a shared read lock (concurrent). All mutations hold an exclusive write lock. Balance reservation and sell-position reservation are atomic — concurrent order placement cannot race past zero.

**Multi-pod path:** implement `RedisLockProvider : ILockProvider` + `RedisTradingEngine : ITradingEngine`, swap in DI. No other code changes needed.

---

## Security

| Layer | What's applied |
|---|---|
| **Rate limiting** | Global: 100 req/min per IP. POST `/api/orders`: 20 req/min per IP. Returns `429 Too Many Requests`. |
| **Security headers** | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-XSS-Protection: 0` |
| **CORS** | Explicit allow-list via `AllowedOrigins` env var. Defaults to `http://localhost:5173`. |
| **Input validation** | FluentValidation on every POST — pair must be supported, `limitPrice > 0`, `quantity > 0`. Returns `400` with `ValidationProblem`. |
| **Business rules** | BUY rejected with `422` if balance < quantity. SELL rejected with `422` if position < quantity (prevents naked short-selling). |
| **Exception handling** | Unhandled exceptions return `application/problem+json` (RFC 7807) — no stack traces in responses. |
| **Swagger UI** | Available **only in Development**. Not exposed in production. |

---

## API

### Swagger UI (dev only)

```
http://localhost:5000/swagger
```

### Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/rates` | Live rates for all three pairs |
| `GET` | `/api/orders` | All orders, newest first |
| `POST` | `/api/orders` | Place a limit order |
| `DELETE` | `/api/orders/{id}` | Cancel a pending order |
| `GET` | `/api/positions` | Open positions with unrealised P&L |
| `GET` | `/api/account` | Balance and currency |

### Place order request

```json
{
  "pair": "USD/EUR",
  "side": "Buy",
  "limitPrice": 0.9180,
  "quantity": 1000
}
```

**`side`** values: `"Buy"` · `"Sell"`

### Order fills

| Side | Fills when |
|---|---|
| Buy | `currentRate ≤ limitPrice` |
| Sell | `currentRate ≥ limitPrice` |

The matching engine evaluates every 500 ms. Place at or near the live market rate (click **MARKET** in the UI) for near-immediate fills.

---

## Logging

Uses `ILogger<T>` throughout. Levels:

| Level | Where |
|---|---|
| `Information` | Order placed / filled / cancelled; background services start/stop |
| `Warning` | Order rejected (insufficient balance or position) |
| `Debug` | Each order matched by the matching loop |
| `Trace` | Each rate tick from the simulator (high-frequency, off by default) |

Configure minimum level in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "FxSandbox": "Debug"
    }
  }
}
```

---

## Running locally

```bash
# Backend
dotnet run --project src/FxSandbox.Api
# http://localhost:5000  |  Swagger: http://localhost:5000/swagger

# Frontend
cd fx-sandbox-ui && npm install && npm run dev
# http://localhost:5173

# Tests
dotnet test
cd fx-sandbox-ui && npm test
```

---

## Deploying to Railway

1. Create project at [railway.com/dashboard](https://railway.com/dashboard), link this repo.
2. Add GitHub secrets: `RAILWAY_TOKEN` (from Railway Account Settings → Tokens) and `RAILWAY_SERVICE_ID` (from the service's Variables tab).
3. Push to `main` → CI tests → deploy runs automatically.

The `Dockerfile` builds React into `wwwroot/`, then serves API + SPA from a single container on port **8080**.

### Environment variables

| Variable | Default | Description |
|---|---|---|
| `AllowedOrigins` | `http://localhost:5173` | Comma-separated CORS allow-list |
| `ASPNETCORE_URLS` | `http://+:8080` | Set in Dockerfile |
| `RAILWAY_TOKEN` | — | GitHub secret for deploy workflow |
| `RAILWAY_SERVICE_ID` | — | GitHub secret — targets the correct Railway service |
