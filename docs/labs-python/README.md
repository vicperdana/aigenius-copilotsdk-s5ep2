# Python labs

Hands-on exercises for the **AI Genius S5E2 — Agent HQ Demo**, focused on the
**GitHub Copilot SDK**. This is the **Python track**: pick **one** track —
Python or [.NET](../labs/) — and work through it in order. Both stacks can run
at once because Python uses **5070** and .NET uses **5050/5051**.

## Prerequisites

| Requirement | Notes |
|:------------|:------|
| [Python 3.11+](https://www.python.org/downloads/) | `python3 --version` should report `3.11` or later |
| [uv](https://docs.astral.sh/uv/getting-started/installation/) | Installs dependencies and runs the Python commands |
| [GitHub Copilot CLI](https://docs.github.com/copilot) | Signed in with Copilot access; the SDK talks to this |
| `git`, `curl`, `jq` | `curl` and `jq` are used in API checks and extras |
| A terminal + editor | VS Code recommended — the repo ships `.vscode/mcp.json` |

Verify before you start:

```bash
python3 --version   # 3.11+
uv --version
copilot --version   # 1.x
```

Core Python commands, run from the app directory:

```bash
cd src/AgentOrchestrator-python
uv sync
uv run uvicorn app.main:app --port 5070   # API + UI on one port
uv run pytest                             # 33 tests
uv run ruff check .
```

## 🎯 The SDK path

The core route. Roughly two hours end to end.

| # | Lab | What you'll do | Time |
|:--|:----|:---------------|:-----|
| 01 | [Setup](01-setup/) | Install dependencies, run the FastAPI app, and smoke-test the SDK samples | ~15 min |
| 02 | [First chat](02-first-chat/) | Stream a response; discover models at runtime | ~20 min |
| 03 | [Tools](03-tools/) | Let the model call your Python with `@define_tool` | ~20 min |
| 04 | [Events](04-events/) | Read the real session event lifecycle | ~20 min |
| 05 | [Sessions](05-sessions/) | Persist and resume a conversation across restarts | ~20 min |
| 06 | [MCP](06-mcp/) | Attach an MCP server for tools you didn't write | ~20 min |
| 07 | [Wrap-up](07-wrap-up/) | Consolidate, clean up, pick a next step | ~10 min |

### Runnable samples

Labs 03–06 are backed by real Python modules, one subcommand per lab:

```bash
cd src/AgentOrchestrator-python
uv run python -m sdk_labs tools
uv run python -m sdk_labs events
uv run python -m sdk_labs sessions
uv run python -m sdk_labs mcp
```

The sample commands are backed by real modules in `sdk_labs`. Model text and
diagnostic warnings can vary by account, SDK version, and local Copilot CLI
settings.

## 📎 Extra labs — not the SDK

Useful, but they cover **Copilot CLI** and general app development rather than
the SDK. Optional, and independent of the numbered path.

| Lab | Covers | Why it's extra |
|:----|:-------|:---------------|
| [Extend the API](extra-extend-api/) | FastAPI, SQLModel, pytest | Copilot as a coding assistant; touches no SDK |
| [Custom agents](../labs/extra-custom-agents/) | `.agent.md` files, agent-assisted review | A Copilot CLI feature shared by both tracks — filed under [Breakouts](../breakouts/) |
| [Governance hooks](../labs/extra-governance-hooks/) | Shell hooks, security gate, audit log | A Copilot CLI feature; the SDK equivalent is in [Lab 03](03-tools/) |

Only [Extend the API](extra-extend-api/) is Python-specific. Custom agents and
governance hooks are language-agnostic CLI labs, so the site lists them under
[Breakouts → Hands-on extras](../breakouts/) rather than duplicating them per
track.

## Conventions

- Commands are **copy-pasteable** from the repository root unless a lab says to `cd src/AgentOrchestrator-python`
- Expected output is shown so you can confirm each step
- ⚠️ marks something that will bite you if skipped
- 💡 marks optional extra credit

## ⚠️ Before you "fix" anything

The Python implementation **intentionally** contains four flawed code patterns in
[`app/services/retail_analytics.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/services/retail_analytics.py), used for code-review demonstrations:

- N+1 query in `get_transactions_with_segments`
- Missing null check in `get_transaction`
- No input validation in `add_transaction`
- Hardcoded threshold in `predict_segment`

They mirror the .NET smells exactly, so the same answer key applies to both
tracks. [Extra — Custom agents](../labs/extra-custom-agents/) asks you to
*find* them. Do not repair them — the review exercises rely on them still being
there. See [`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md).

## Related

- [Python app README](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/README.md)
- [.NET track](../labs/)
- [Demos](../demos-python/) — walkthroughs of the Python code these labs touch
- [Breakouts](../breakouts/) — architecture, agents, hooks, and troubleshooting
- [Troubleshooting](../breakouts/troubleshooting.md) — start here when a step fails
