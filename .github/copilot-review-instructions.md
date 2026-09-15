# Copilot Code Review Instructions

## Project Context

This is a .NET 10 retail transaction analytics demo built on the **GitHub
Copilot SDK v1.0.13**. It has two projects: an ASP.NET Core Web API
(`AgentHQDemo.Api`, port 5050) that streams chat over SSE and serves
transaction/segment REST endpoints, and a Blazor WebAssembly front end
(`AgentHQDemo.Web`, port 5051). Data is SQLite via EF Core, auto-created and
seeded on startup.

## ⚠️ Intentional Demo Code Smells — Do Not Flag as Bugs

These exist **on purpose** to be discovered live during review demos. Flag
them only when the reviewer is explicitly hunting for them:

- N+1 query in `GetTransactionsWithSegmentsAsync`
- Missing null check in `GetTransactionAsync`
- No input validation in `AddTransactionAsync`
- Hardcoded threshold in `PredictSegmentAsync`

Anything **not** on this list should be reviewed normally.

## Review Focus Areas

### Copilot SDK Usage

- Namespace is `GitHub.Copilot` — **not** `GitHub.Copilot.SDK`. That moved in
  v1.0.0; flag any reintroduction of the old namespace.
- `session.On<T>(...)` requires an explicit type argument; the non-generic
  form no longer infers.
- `CopilotClient` must be disposed — the service implements `IAsyncDisposable`
  and holds a single long-lived client.
- Model IDs must not be hardcoded in new code. The available list is fetched
  at runtime via `ListModelsAsync()`; a stale hardcoded list is what broke the
  model picker previously.
- Connection loss should reset `_isStarted` so `EnsureStartedAsync()` can
  rebuild the client.

### SSE Streaming

- `ChatController.StreamChat` must flush after every chunk
  (`Response.Body.FlushAsync`) or the UI will buffer instead of streaming.
- Always honour `CancellationToken` — an abandoned browser tab must not leave
  a session running.
- Errors mid-stream are emitted as `data: {"error": ...}`, not as an HTTP
  status, because headers are already sent.
- The client parses `data: [DONE]` as the terminator; don't change one side
  without the other.

### C# Conventions

- File-scoped namespaces, nullable reference types enabled
- `record` types for DTOs, collection expressions `[]` over `new List<T>()`
- Async methods end in `Async` and take a `CancellationToken`
- Private fields `_camelCase`, interfaces prefixed `I`

### EF Core / SQLite

- `decimal` on SQLite has no native type — verify conversions round-trip
- Watch for queries that materialise before filtering (`.ToList()` then
  `.Where()`)
- Seeding must stay idempotent; it runs on every startup

### Build & CI

- `Directory.Build.props` auto-detects a locally installed Copilot CLI so the
  build works when `registry.npmjs.org` is unreachable. Don't remove the
  fallback to the SDK's normal download — that path is what CI and Codespaces
  use.
- CodeQL runs through default setup, configured from the repository's Security
  tab. There is no `codeql.yml` to edit, so don't add one back.
- Keep actions off the deprecated Node 20 runtime.

### Security

- No secrets, API keys, or credentials in code — use environment variables
- `.env` is gitignored; never commit it or reintroduce it
- No customer names, competitive analysis, or internal-only material in code,
  docs, or commit messages
- Validate user input on API endpoints (except the documented demo smell)
