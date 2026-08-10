# Labs

Hands-on exercises for the **AI Genius S5E2 — Agent HQ Demo**, focused on the
**GitHub Copilot SDK**. Work through the SDK path in order; each lab builds on
the previous one.

## Prerequisites

| Requirement | Notes |
|:------------|:------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | `dotnet --version` should report `10.x` |
| [GitHub Copilot CLI](https://docs.github.com/copilot) | `npm install -g @github/copilot`, signed in with Copilot access |
| `git`, `curl`, `jq` | `jq` is used by the optional governance-hooks lab |
| A terminal + editor | VS Code recommended — the repo ships `.vscode/mcp.json` |

Verify before you start:

```bash
dotnet --version     # 10.x
copilot --version    # 1.x
```

## 🎯 The SDK path

The core route. Roughly two hours end to end.

| # | Lab | What you'll do | Time |
|:--|:----|:---------------|:-----|
| 01 | [Setup](01-setup/) | Build and run the app and the SDK samples project | ~15 min |
| 02 | [First chat](02-first-chat/) | Stream a response; discover models at runtime | ~20 min |
| 03 | [Tools](03-tools/) | Let the model call your C# with `CopilotTool.DefineTool` | ~20 min |
| 04 | [Events](04-events/) | Read the session event lifecycle — all 33 of them | ~20 min |
| 05 | [Sessions](05-sessions/) | Persist and resume a conversation across restarts | ~20 min |
| 06 | [MCP](06-mcp/) | Attach an MCP server for tools you didn't write | ~20 min |
| 07 | [Wrap-up](07-wrap-up/) | Consolidate, clean up, pick a next step | ~10 min |

### Runnable samples

Labs 03–06 are backed by a real console project, one subcommand per lab:

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- tools
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- events
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- sessions
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- mcp
```

Every command in these labs was executed against the real Copilot CLI and the
output pasted in as-is.

## 📎 Extra labs — not the SDK

Useful, but they cover **Copilot CLI** and general app development rather than
the SDK. Optional, and independent of the numbered path.

| Lab | Covers | Why it's extra |
|:----|:-------|:---------------|
| [Custom agents](extra-custom-agents/) | `.agent.md` files, agent-assisted review | A CLI feature, not the SDK |
| [Governance hooks](extra-governance-hooks/) | Shell hooks, security gate, audit log | A CLI feature; the SDK equivalent is in [Lab 03](03-tools/) |
| [Extend the API](extra-extend-api/) | ASP.NET Core, EF Core, xUnit | Copilot as a coding assistant; touches no SDK |

## Conventions

- Commands are **copy-pasteable** from the repository root
- Expected output is shown so you can confirm each step
- ⚠️ marks something that will bite you if skipped
- 💡 marks optional extra credit

## ⚠️ Before you "fix" anything

This repository **intentionally** contains four flawed code patterns used for
code-review demonstrations:

- N+1 query in `GetTransactionsWithSegmentsAsync`
- Missing null check in `GetTransactionAsync`
- No input validation in `AddTransactionAsync`
- Hardcoded threshold in `PredictSegmentAsync`

[Extra — Custom agents](extra-custom-agents/) asks you to *find* them. Do not
repair them — the demo script relies on them still being there. See
[`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md).

## Related

- [Demos](../demos/) — walkthroughs of the code these labs touch
- [Breakouts](../breakouts/) — architecture, agents, hooks, and troubleshooting
- [Troubleshooting](../breakouts/troubleshooting.md) — start here when a step fails
