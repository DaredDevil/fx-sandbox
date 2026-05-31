# FX Sandbox — Paper Trading Terminal

A full-stack foreign-exchange paper trading simulator. Traders can experiment with limit orders on USD/EUR, USD/GBP, and USD/CHF without touching real markets. The system starts with USD 10,000 of capital, simulates a live rate feed, and fills limit orders automatically when the market price reaches the limit.

---

## Architecture

```
fx-sandbox/
├── FxSandbox.Api/          # ASP.NET 8 minimal-API backend (in-memory, no DB)
│   ├── Domain/             # OrderStatus, OrderSide, LimitOrder, Position value objects
│   ├── Features/Orders/    # PlaceOrderRequest + FluentValidation validator
│   └── Services/
│       ├── TradingEngine          # Thread-safe in-memory state (orders, positions, rates, balance)
│       ├── RateSimulatorService   # BackgroundService: random-walk rate ticks every 500 ms
│       └── OrderMatchingService   # BackgroundService: fills pending orders every 500 ms
├── FxSandbox.Tests/        # xUnit test suite
│   ├── TradingEngineTests.cs      # Unit tests — engine logic, P&L, fill rules
│   ├── PlaceOrderValidatorTests.cs # Unit tests — FluentValidation rules
│   └── ApiIntegrationTests.cs     # Integration tests — HTTP endpoints via WebApplicationFactory
├── fx-sandbox-ui/          # React 18 + Vite + TailwindCSS frontend
│   └── src/
│       ├── api/            # Typed fetch client + TypeScript interfaces
│       └── features/
│           ├── rates/      # RatesTicker (live flash animation)
│           ├── orders/     # PlaceOrderForm, OrderBook (with cancel)
│           └── positions/  # PositionsPanel (unrealised P&L)
├── Dockerfile              # Multi-stage: Node → React build → .NET runtime
└── railway.toml            # Railway deployment config
```

---

## Rate Simulation

Each pair ticks every **500 ms** using a random walk:

```
newRate = oldRate × (1 + Δ)   where Δ ∈ [−0.005, +0.005]
```

Seeds (initialised once at startup):

| Pair    | Seed   |
|---------|--------|
| USD/EUR | 0.9185 |
| USD/GBP | 0.7890 |
| USD/CHF | 0.8990 |

---

## Order Matching

The `OrderMatchingService` evaluates every pending order after each rate tick:

| Side | Fills when…               |
|------|---------------------------|
| Buy  | `currentRate ≤ limitPrice` |
| Sell | `currentRate ≥ limitPrice` |

**Tip:** set your limit price close to the live market rate shown in the order form — the "MARKET" button pre-fills it for you.

---

## API Endpoints

| Method   | Path                 | Description                          |
|----------|----------------------|--------------------------------------|
| `GET`    | `/api/rates`         | Current rates for all three pairs    |
| `GET`    | `/api/orders`        | All orders (newest first)            |
| `POST`   | `/api/orders`        | Place a limit order                  |
| `DELETE` | `/api/orders/{id}`   | Cancel a pending order               |
| `GET`    | `/api/positions`     | Open positions with unrealised P&L   |
| `GET`    | `/api/account`       | Account balance                      |

### Place order body

```json
{
  "pair": "USD/EUR",
  "side": "Buy",
  "limitPrice": 0.9180,
  "quantity": 1000
}
```

Validation rules: `pair` must be one of the three supported pairs; `limitPrice` and `quantity` must be `> 0`.

---

## Running locally

### Backend

```bash
dotnet run --project FxSandbox.Api
# listens on http://localhost:5000
```

### Frontend

```bash
cd fx-sandbox-ui
npm install
npm run dev
# opens http://localhost:5173
```

### Tests

```bash
# Backend
dotnet test

# Frontend
cd fx-sandbox-ui && npm test
```

---

## Deploying to Railway

### One-time setup

1. Create a new project at [railway.com/dashboard](https://railway.com/dashboard).
2. Add a **service** linked to this GitHub repository.
3. Railway auto-detects the `Dockerfile` via `railway.toml`.
4. Add an environment variable in Railway:
   - `AllowedOrigins` — set to your Railway public URL if you deploy the UI separately (not needed when the API serves the static files).
5. Add a GitHub secret `RAILWAY_TOKEN` (from *Account Settings → Tokens* on Railway).

### Automated deploys

Push to `main` → CI runs tests (`ci.yml`) → if they pass, `deploy.yml` deploys to Railway automatically.

The deploy workflow (`deploy.yml`):
1. Re-runs the full dotnet test suite as a gate.
2. Installs the Railway CLI.
3. Runs `railway up --service fx-sandbox --detach`.

### What Railway runs

The `Dockerfile` builds in three stages:
1. **Node 22** — `npm ci && npm run build` (React → `dist/`)
2. **dotnet SDK 8** — `dotnet publish` (Release)
3. **dotnet ASP.NET 8 runtime** — copies published API + React `dist/` into `wwwroot/`

The single container listens on port **8080** and serves both the API (`/api/*`) and the React SPA (all other paths → `index.html`).

---

## Environment variables

| Variable          | Default              | Description                                      |
|-------------------|----------------------|--------------------------------------------------|
| `AllowedOrigins`  | `http://localhost:5173` | Comma-separated list of allowed CORS origins  |
| `ASPNETCORE_URLS` | `http://+:8080`      | Set automatically in the Dockerfile              |
| `RAILWAY_TOKEN`   | —                    | GitHub secret used by the deploy workflow        |
