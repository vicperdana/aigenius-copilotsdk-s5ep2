# AI Agent Guidelines

This file contains instructions and guidelines for AI agents working on this
repository — GitHub Copilot, Claude, Codex, and any custom agents.

> Coding standards and architecture live in
> [`.github/copilot-instructions.md`](.github/copilot-instructions.md).
> This file covers repository conduct: what to commit, what to leave alone,
> and how to handle issues.

## 🔒 Security Best Practices

**Never commit sensitive information to this repository:**

- API keys, tokens, or credentials
- Personal access tokens (PATs)
- Database connection strings with passwords
- Environment-specific configuration values
- Customer names, competitive analysis, or any internal-only material

`.env` is gitignored and must stay that way. No environment file is committed
to this repository — document required variables in the README instead of
checking in a sample file.

**For MCP configuration files (`mcp.json`):**

- Use placeholder values like `"YOUR_API_KEY_HERE"` or `"${API_KEY}"`
- Reference environment variables for sensitive data
- Document which environment variables are required

## 📋 Repository Guidelines

### Purpose

This repository is demo content for the **AI Genius S5E2** session. It should:

- Provide clear, runnable content for session attendees
- Support self-guided learning for people working through it later
- Stay reproducible — both tracks must pass from a clean clone:
  - .NET: `dotnet build` and `dotnet test` on `src/AgentOrchestrator/AgentHQDemo.slnx`
  - Python: `uv sync`, `uv run ruff check .`, and `uv run pytest` in `src/AgentOrchestrator-python`

### Two implementations, one lesson

The same retail analytics app exists twice — `src/AgentOrchestrator/` (.NET) and
`src/AgentOrchestrator-python/` (Python). They deliberately mirror each other:
same endpoints, same camelCase JSON contract, same seed data, the same 14 domain
tests, the same read-only MCP server tool surface, the same model-visibility
filter, and the same four intentional code smells.

**When you change behaviour in one, change it in the other**, or the labs drift
apart. Purely idiomatic changes (a C#-only refactor, a Python-only lint fix) do
not need mirroring.

### What NOT to modify without permission

- License files (`LICENSE`, `LICENSE-DOCS`, `CODE_OF_CONDUCT.md`)
- Security files (`SECURITY.md`)
- GitHub workflow files in `.github/workflows/`

### Content Rules

- No large binary files (PowerPoint decks, videos, recordings) in the repo —
  link to them instead
- All README files should be kept up to date
- Unused folders containing only a placeholder README should be removed

### Intentional Demo Code Smells

This repo **deliberately** contains code issues used to demonstrate code
review and static analysis. See *Security Notes* in the [README](README.md).
**Do not "fix" these without checking first** — they are the demo:

| Flaw | .NET — `RetailAnalyticsService.cs` | Python — `app/services/retail_analytics.py` |
|:-----|:-----------------------------------|:--------------------------------------------|
| N+1 query | `GetTransactionsWithSegmentsAsync` | `get_transactions_with_segments` |
| Missing null check | `GetTransactionAsync` | `get_transaction` |
| No input validation | `AddTransactionAsync` | `add_transaction` |
| Hardcoded threshold | `PredictSegmentAsync` | `predict_segment` |

Note the MCP servers reuse `PredictSegmentAsync` / `predict_segment` rather than
reimplementing the scoring logic, so the hardcoded threshold stays in exactly one
place per track. Do not "helpfully" inline a corrected copy into the MCP server.

### Issue Management

When a user reports a problem, asks a question that should be tracked, or
wants to file an issue:

1. **Discover available templates** — check `.github/ISSUE_TEMPLATE/` for any
   `.yml` or `.md` files and read them to understand the expected fields.
2. **Match the request to a template** — pick the best fit, or create a plain
   issue if none exist.
3. **Help fill in the fields** — walk through required fields interactively,
   proposing answers where possible.
4. **Create the issue** — `gh issue create --template <file>`, or
   `gh issue create` for a plain issue.
5. **Apply labels** — check `gh label list` first; don't apply labels that
   don't exist.

## ✅ Before You Push

Run the checks for whichever track you touched. If you touched both, run both.

**.NET**

- `dotnet build src/AgentOrchestrator/AgentHQDemo.slnx` succeeds with no warnings
- `dotnet test src/AgentOrchestrator/AgentHQDemo.slnx` passes (29 tests)

**Python** — from `src/AgentOrchestrator-python`

- `uv sync` resolves cleanly
- `uv run ruff check .` reports no errors
- `uv run pytest` passes (33 tests: 14 domain + 12 MCP + 3 model visibility + 4 contract)

**Docs** — if you touched anything under `docs/` or `mkdocs.yml`

- `python3 scripts/rewrite_doc_links.py --check` passes — every
  `blob/main` link must point at a file that actually exists
- `python3 scripts/check_mermaid.py` passes
- `mkdocs build --strict` succeeds
- New pages are wired into the `nav:` block in `mkdocs.yml`

**Always**

- No secrets, customer names, or internal material in the diff **or** in
  commit history
