@../Agentic/templates/dotnet/CLAUDE.md
@../Agentic/templates/partials/react-frontend-section.md

## Project-specific overrides

- **Project name:** FX Sandbox
- **Solution:** FxSandbox.sln
- **Run API:** `dotnet run --project FxSandbox.Api`
- **Run frontend:** `cd fx-sandbox-ui && npm run dev`
- **Backend tests:** `dotnet test`
- **Frontend tests:** `cd fx-sandbox-ui && npm test`
- **Frontend subfolder:** `fx-sandbox-ui` (npm, React 18 + Vite)
- **Zod import:** `from 'zod/v4'` not `from 'zod'`
- **Enum serialization:** `JsonStringEnumConverter` is wired in Program.cs - never remove it
- **Thread safety:** all TradingEngine mutations go through `lock (_lock)` - never bypass
- **No database:** in-memory only - do not add EF Core or any persistence
- **Out of scope:** (none beyond template defaults)
