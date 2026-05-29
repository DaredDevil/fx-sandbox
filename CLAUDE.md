# FX Sandbox — Agent Contract

## Stack
- **Backend**: .NET 8 Minimal APIs, Vertical Slice Architecture, in-memory state only
- **Frontend**: React 18 + TypeScript, TanStack Query, React Hook Form + Zod v4, Tailwind CSS v3
- **Tests**: xUnit + FluentAssertions (backend), Vitest + React Testing Library (frontend)

## Commands
| Task | Command |
|------|---------|
| Run API | `dotnet run --project FxSandbox.Api` |
| Backend tests | `dotnet test` |
| Frontend dev | `cd fx-sandbox-ui && npm run dev` |
| Frontend tests | `cd fx-sandbox-ui && npm test` |
| Type check (FE) | `cd fx-sandbox-ui && npm run typecheck` |

## Conventions
- Backend: `lock` object guards all TradingEngine state — never bypass
- Frontend: TanStack Query for all server state; no `useEffect` for data fetching
- Zod imports: `from 'zod/v4'` (not `from 'zod'`)
- No database; all state lives in `TradingEngine` singleton
- CORS locked to `http://localhost:5173`
