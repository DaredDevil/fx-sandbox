# CLAUDE.md — .NET template

> Drop at the **repository root** (next to your `.sln`). Edit `<<EDIT ME>>` placeholders to match the actual repo.

## Architecture

`<<EDIT ME: Vertical Slice Architecture | Clean Architecture | Modular Monolith | Layered | Minimal APIs | CQRS | Match existing>>`

**If Vertical Slice Architecture (default):** organize code by feature in `src/Features/<FeatureName>/<UseCaseName>/`. Each folder contains Request, Validator, Handler, Response, Endpoint, and Tests. Slices don't call each other's handlers — use domain events for cross-slice communication. Shared code lives in `Common/` only when used by 3+ slices.

**Architecture options and guidance:** see `docs/dotnet-workflow.md` in the Agentic shared config repo.

> **Agent instruction:** Before writing any code, ask the developer which architecture to use. Default to Vertical Slice Architecture. If the project has existing code, read it and follow the existing pattern — never introduce a second architecture.

## Stack

- **Runtime/SDK:** .NET 8 (or 9) — pinned via `global.json`
- **Language:** C# with nullable + implicit usings ON, `TreatWarningsAsErrors=true`
- **Test runner:** `<<EDIT ME: xUnit | NUnit | MSTest>>` (xUnit recommended)
- **Format/lint:** `dotnet format` + Roslyn analyzers (`Microsoft.CodeAnalysis.NetAnalyzers`) + StyleCop optional
- **Build:** `dotnet build -c Release`
- **CI:** GitHub Actions (see `.github/workflows/ci.yml`)

## Commands the agent must use

| Purpose            | Command                                                                |
| ------------------ | ---------------------------------------------------------------------- |
| Restore            | `dotnet restore`                                                       |
| Build              | `dotnet build --no-restore -c Release`                                 |
| Run unit tests     | `dotnet test --no-build -c Release --logger "trx"`                     |
| Coverage           | `dotnet test --collect:"XPlat Code Coverage"`                          |
| Format check       | `dotnet format --verify-no-changes --severity warn`                    |
| Format apply       | `dotnet format`                                                        |
| Analyzers as errors| Already enforced via `Directory.Build.props` (`TreatWarningsAsErrors`) |
| Run app            | `dotnet run --project src/<<EDIT ME: Project.Name>>`                   |

## Repository conventions

- **Solution layout:** `src/<Project>` for production code; `tests/<Project>.Tests` for tests. One test project per production project.
- **One class per file.** Filename matches the public type.
- **`Directory.Build.props` at the solution root** sets shared settings: nullable, implicit usings, target framework, warnings-as-errors, analyzer level. Individual `.csproj` files inherit; do not duplicate these settings per project.
- **No `var` for primitive types in public APIs.** OK inside method bodies.
- **Async by default** for I/O. Method names end in `Async`. Return `Task` / `ValueTask`, accept `CancellationToken` as the last parameter.
- **No `Thread.Sleep`** in production code. Use `await Task.Delay(...)`.
- **DI registration** lives in `Program.cs` (Web/Worker) or a single `ServiceCollectionExtensions.cs` per project. Don't sprinkle `AddSingleton` calls across the codebase.
- **Logging** via `ILogger<T>`. Never `Console.WriteLine` outside `Program.cs`.
- **Secrets** via User Secrets in dev, environment variables / Key Vault in prod. Never commit `appsettings.Development.json` with real values.

## Out of scope for the agent

Do **not** modify:

- `<<EDIT ME: paths like infra/, db/migrations/, terraform/>>`
- `Directory.Build.props` / `global.json` / `.editorconfig` — unless the ticket is explicitly about tooling
- `*.csproj` package versions — bumping deps is its own ticket
- `.github/workflows/` unless the ticket is about CI

> Onboarding steps (read-all, infer conventions, pre-file checklist) live in ~/.claude/CLAUDE.md and apply automatically to every project.

## How the agent should work in this repo

1. **Confirm architecture first.** Ask the developer which architecture to use (see above) before writing any code.
2. **Read the Jira ticket** before planning. Reference its key in commit messages and PR titles.
3. **Plan-mode first** for anything that touches more than one project or more than ~50 lines.
4. **TDD when feasible:** write the test in `tests/<Project>.Tests`, watch it fail, then implement. Use xUnit + FluentAssertions + WebApplicationFactory for integration tests.
5. **No mock databases.** Use Testcontainers for integration tests — same engine as production. Unit tests test pure logic only.
6. **FluentValidation for all requests.** Wire as a MediatR pipeline behavior. Never validate inside the handler.
7. **Problem Details for all error responses.** Return `application/problem+json` with RFC 9457 structure.
8. **`RequireAuthorization()` on every new endpoint.** Read user identity from `HttpContext.User` claims.
9. **Run `dotnet format && dotnet build && dotnet test` before committing.** The pre-commit hook enforces this.
10. **Commit message style:** Conventional Commits — `feat(orders): add idempotency key (PROJ-123)`.
11. **PR draft → ready-for-review** after CI is green and review subagents agree.

## What the commit-time guard runs

```
dotnet format --verify-no-changes  →  dotnet build  →  dotnet test
```

If any step fails, commit is blocked. The hook is in `.claude/hooks/guard-commit.ps1`.

## Notes

- This repo uses a `Directory.Build.props` at root — see [`Directory.Build.props.example`](Directory.Build.props.example) for the recommended baseline.
- `.editorconfig` drives both formatting and analyzer severity. Don't bypass it with `#pragma warning disable` in production code unless you also add a comment explaining why and link to a follow-up ticket.

---

## Frontend (React + TypeScript)

> Applies to the `<<EDIT ME: fx-sandbox-ui | client | frontend>>` subfolder.

- **Package manager:** `<<EDIT ME: npm | pnpm>>`
- **State:** TanStack Query for server state; `useState` for local UI state
- **Forms:** React Hook Form + Zod (`import { z } from 'zod/v4'` - not `from 'zod'`)
- **Tests:** Vitest + React Testing Library

| Purpose | Command |
| --- | --- |
| Dev server | `cd <<EDIT ME: subfolder>> && <<PM>> run dev` |
| Unit tests | `cd <<EDIT ME: subfolder>> && <<PM>> test` |
| Type-check | `cd <<EDIT ME: subfolder>> && <<PM>> run typecheck` |
| Full check | `cd <<EDIT ME: subfolder>> && <<PM>> run check` |

### Frontend conventions
- TanStack Query for all server state - no `useEffect` for data fetching
- `src/features/<feature>/` - component + test co-located
- `src/api/` - HTTP client and shared type interfaces only
- No `any` - use the interfaces in `src/api/types.ts`
- Named exports everywhere
- Tailwind only for styling - no inline styles

---

## Project-specific overrides
<!-- Add project-specific rules below this line.
     These are preserved when you run sync-standards.
     Examples: project name, run command, out-of-scope paths -->

