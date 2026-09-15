# AI Instructions for Agent HQ Demo

This file provides context and coding guidelines for **all AI assistants** working in this repository—including GitHub Copilot, Claude, Codex, and custom agents.

## Project Overview

This is a **Retail Transaction Analytics** app demonstrating modern AI-assisted development:
- Retail transactions, customer segmentation, and segment prediction
- Multi-agent workflows (Copilot, Claude, Codex working together)
- Autonomous coding agents that plan, implement, and iterate
- Enterprise governance with audit trails and policy controls
- SQLite for zero-config data persistence

It is implemented **twice** — once in .NET 10 LTS and once in Python 3.11+ —
so learners can follow whichever track they prefer. The two are behavioural
mirrors: same endpoints, same camelCase JSON contract, same seed data, same
14 domain tests, same read-only MCP server tool surface, same model-visibility
filter, same four intentional code smells. Change behaviour in one and you must
change it in the other.

## Repository Structure

```
src/
├── AgentOrchestrator/              # .NET track
│   ├── AgentHQDemo.Api/            # ASP.NET Core Web API
│   │   ├── Controllers/            # Chat, Transactions, Segments endpoints
│   │   ├── Data/                   # EF Core DbContext (SQLite)
│   │   ├── Services/               # RetailAnalyticsService, CopilotChatService
│   │   └── Models/                 # Transaction, CustomerSegment, SegmentPrediction
│   ├── AgentHQDemo.McpServer/      # Read-only MCP server over retail.db
│   ├── AgentHQDemo.Web/            # Blazor WebAssembly UI
│   ├── samples/SdkLabs/            # Runnable lab samples
│   └── tests/AgentHQDemo.Tests/    # xUnit tests (29)
└── AgentOrchestrator-python/       # Python track
    ├── app/
    │   ├── routers/                # chat, transactions, segments
    │   ├── services/               # retail_analytics, copilot_chat
    │   ├── models.py               # SQLModel entities
    │   ├── database.py             # engine + session dependency
    │   ├── main.py                 # FastAPI app, lifespan seed, static mount
    │   └── static/                 # HTML + vanilla JS chat UI
    ├── mcp_server/                 # Read-only MCP server over retail.db
    ├── sdk_labs/                   # Runnable lab samples
    └── tests/                      # pytest tests (33)
.github/
├── agents/                         # Custom agent definitions
├── workflows/                      # CI/CD pipelines
└── copilot-instructions.md         # This file (teaches ALL agents)
```

## Coding Standards (All Agents Should Follow)

### C# Conventions
- Use **file-scoped namespaces** (C# 10+)
- Prefer **primary constructors** for simple DI (C# 12+)
- Use **collection expressions** `[]` over `new List<T>()`
- Enable **nullable reference types** — no `null` without `?`
- Use `record` types for DTOs and immutable data

### Async Patterns
- All I/O operations must be `async`
- Use `CancellationToken` in all async methods
- Prefer `ValueTask` for hot paths that often complete synchronously
- Always use `ConfigureAwait(false)` in library code

### Naming Conventions
- Async methods end with `Async` suffix
- Private fields use `_camelCase`
- Constants use `PascalCase`
- Interfaces start with `I` prefix

### Error Handling
- Use `Result<T>` pattern over exceptions for expected failures
- Log structured data with `ILogger<T>`
- Return `ProblemDetails` for API errors

### Python Conventions
- Target **Python 3.11+**; manage the project with [uv](https://docs.astral.sh/uv/)
- Lint and format with **Ruff** — `uv run ruff check .`
- Full type hints on every public function; prefer `X | None` over `Optional[X]`
- **SQLModel** for entities. Note the trap: SQLModel skips validation on
  `table=True` classes, so constrained fields belong on a shared base class
  (`TransactionBase`) that both the table and the request model inherit
- Keep the wire format **camelCase** via Pydantic
  `ConfigDict(alias_generator=to_camel, populate_by_name=True)` — this preserves
  the .NET HTTP contract so the same `curl` works against either stack
- `async def` for all I/O; never block the event loop
- Dependencies via FastAPI `Depends`, routers as `APIRouter` in `app/routers/`
- Mount `StaticFiles` **last** in `app/main.py` so it does not shadow `/api`
- Private module-level names use a leading underscore

### Python Copilot SDK Notes
- Package `github-copilot-sdk`, import root `copilot`
- `CopilotClient()` supports `async with`; otherwise `await client.start()`
- `await client.create_session(...)` is keyword-only
- `session.on(handler)` returns an unsubscribe callable — events are
  **push-only**, so bridge them into an `asyncio.Queue` to expose a stream
  (the analogue of C#'s `Channel`)
- `SessionEvent` is a **single dataclass** — branch on `evt.type`
  (a `SessionEventType` enum), not on subclasses as in C#
- ⚠️ Custom tools **require** `on_permission_request` or the call is denied.
  The .NET SDK needs no handler. Pass `PermissionHandler.approve_all` in samples
- No `GHCP001` suppression is needed — `copilot.rpc` decisions are not gated
- `PermissionInvocation` is imported from `copilot.session`, not the package root

## MCP Server Notes

Both tracks ship a **read-only** MCP server over `retail.db` so the chat can
answer from real data. The REST API keeps its direct ORM access — MCP is for the
model, not for the app talking to its own database.

- .NET: `AgentHQDemo.McpServer` uses the `ModelContextProtocol` package with
  `[McpServerToolType]` / `[McpServerTool]` and `WithToolsFromAssembly()`
- Python: `mcp_server/` uses the `mcp` package. ⚠️ **2.x renamed `FastMCP` to
  `MCPServer`** (`from mcp.server.mcpserver import MCPServer`), so v1 examples
  found online will not run
- Read-only is enforced at the connection — `Mode=ReadOnly` in .NET,
  `?mode=ro&uri=true` in Python — not merely by omitting writes
- stdio carries the protocol on **stdout**, so all logging must go to stderr
- Expose domain tools, never a generic `run_query`. The tool surface is the
  security boundary
- In the Python stdio config use `working_directory`, not `cwd`; the SDK renames
  it on the way to the wire format

## Multi-Agent Collaboration

This repo supports multiple AI agents working together:

### Available Agents
- **@copilot** — Fast code completion and general assistance
- **@claude** — Deep reasoning and security analysis
- **@codex** — Code generation and documentation
- **@dotnet-reviewer** — .NET-specific code review
- **@security-scanner** — OWASP Top 10 vulnerability detection

### When to Use Each Agent
| Task | Recommended Agent |
|------|-------------------|
| Quick code completion | @copilot |
| Architecture analysis | @claude |
| Security review | @claude or @security-scanner |
| Performance optimization | @copilot |
| Documentation | @codex |
| .NET best practices | @dotnet-reviewer |

### Collaboration Example
```
# In a PR comment:
@copilot implement rate limiting for this endpoint
@claude review security implications
@dotnet-reviewer check for .NET anti-patterns
```

## Agent Development Guidelines

### Copilot SDK Patterns
When creating agents:
```csharp
await using var client = new CopilotClient();
await client.StartAsync();

var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5",  // or claude-opus-4.5, gemini-2.5-pro
    Tools = [ /* AIFunctionFactory tools */ ],
    CustomAgents = [ /* Domain-specific agents */ ]
});
```

### Tool Definitions
- Use `AIFunctionFactory.Create()` for tool registration
- Include `[Description]` attributes on all parameters
- Keep tools focused — one responsibility each
- Handle tool failures gracefully with fallback responses

## Testing Requirements

- Unit tests required for all agent logic
- Integration tests for full SDK flow (requires auth)
- Test both success and failure paths
- Keep the two suites at parity — 14 domain tests, 12 MCP server tests, and
  3 model-visibility tests each.
  Python adds 4 contract
  tests (`test_chat_contract.py`) guarding the static UI's request shape, which
  .NET does not need because its Blazor client is strongly typed.

**.NET** — mock `CopilotClient`; use `FluentAssertions` for readable assertions.

**Python** — pytest with an in-memory SQLite engine and `StaticPool` (see
`tests/conftest.py`); use `pytest.raises` for failure paths.

## Common Commands

```bash
# --- .NET track ---

# Build the solution
dotnet build src/AgentOrchestrator/AgentHQDemo.slnx

# Run tests
dotnet test src/AgentOrchestrator/AgentHQDemo.slnx

# Run the MCP server standalone (normally spawned by CopilotChatService)
dotnet run --project src/AgentOrchestrator/AgentHQDemo.McpServer

# Run the API locally
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Api

# Watch mode for development
dotnet watch --project src/AgentOrchestrator/AgentHQDemo.Api
```

```bash
# --- Python track (from src/AgentOrchestrator-python) ---

# Install dependencies
uv sync

# Lint
uv run ruff check .

# Run tests
uv run pytest

# Run the API and UI together on 5070
uv run uvicorn app.main:app --port 5070

# Watch mode for development
uv run uvicorn app.main:app --port 5070 --reload

# Run a lab sample
uv run python -m sdk_labs tools|events|sessions|mcp|permissions

# Run the MCP server standalone (normally spawned by CopilotChatService)
uv run python -m mcp_server
```

```bash
# --- Docs ---
python3 scripts/rewrite_doc_links.py --check
python3 scripts/check_mermaid.py
mkdocs build --strict
```

## When Asked About This Project

If someone asks "How do I run this?" or "How does this work?":
1. Ask which track they want — .NET or Python — then point at the matching
   quick start. Both stacks can run at once; the ports differ (5050/5051 vs 5070)
2. Point them to this file for conventions
3. Explain the multi-agent architecture
4. Reference the custom agents in `.github/agents/`
5. Mention the demo showcases autonomous AI development

## Security Considerations

- Never commit secrets or API keys
- Use environment variables for sensitive configuration
- Validate all user input in API endpoints
- Sanitize file paths in MCP filesystem operations
- Log agent actions for audit trail compliance
- All AI-generated code must pass CodeQL scanning
