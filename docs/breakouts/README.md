# Breakouts

Track-agnostic material: reference pages you look things up in and the two
Copilot CLI labs that apply equally to .NET and Python.

## Reference

| Doc | Use it when |
|:----|:------------|
| [Architecture](architecture.md) | You need the system diagram, request sequence, or data model |
| [System map](../system-map/) | You want to explore that architecture interactively |
| [Custom agents](custom-agents.md) | You're writing or invoking an `.agent.md` |
| [Hooks and governance](hooks-and-governance.md) | You're configuring `preToolUse` gates or reading the audit trail |
| [Skills](skills.md) | You want to know what the five skills do and when Copilot picks them |
| [Troubleshooting](troubleshooting.md) | **Something broke** — start here |

## Hands-on extras

Optional labs covering **Copilot CLI** rather than the SDK. They're
language-agnostic, so they sit here instead of inside a track — do them from
either the [.NET](../labs/) or the [Python](../labs-python/) path.

| Lab | Covers | Time |
|:----|:-------|:-----|
| [Custom agents](../labs/extra-custom-agents/) | `.agent.md` files, agent-assisted review of the intentional code smells | ~20 min |
| [Governance hooks](../labs/extra-governance-hooks/) | Shell hooks, the security gate, the audit log | ~20 min |

The SDK equivalent of hook-style control is tool definition — see
[.NET Lab 03](../labs/03-tools/) or [Python Lab 03](../labs-python/03-tools/).

## Common problems, fast

| Symptom | See |
|:--------|:----|
| `MSB3923` — can't download the Copilot CLI | [Troubleshooting](troubleshooting.md) |
| CodeQL finds no results | [Troubleshooting](troubleshooting.md) — check default setup is enabled |
| `Model "..." is not available` | [Troubleshooting](troubleshooting.md) |
| Port 5050/5051 already in use | [Troubleshooting](troubleshooting.md) |
| `MSB1003` — no project or solution found | The solution is at `src/AgentOrchestrator/AgentHQDemo.slnx` |
| A hook doesn't seem to run | [Hooks and governance](hooks-and-governance.md) |

## Related

- [.NET labs](../labs/) · [.NET demos](../demos/)
- [Python labs](../labs-python/) · [Python demos](../demos-python/)
- [Root README](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/README.md) — quick start and endpoint reference
- [`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md) — repository rules for AI agents
