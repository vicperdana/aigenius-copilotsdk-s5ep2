# Breakouts

Supplementary reference material — diagrams, configuration reference, and
troubleshooting. Use these to look things up, rather than reading front to
back.

## Reference

| Doc | Use it when |
|:----|:------------|
| [Architecture](architecture.md) | You need the system diagram, request sequence, or data model |
| [Custom agents](custom-agents.md) | You're writing or invoking an `.agent.md` |
| [Hooks and governance](hooks-and-governance.md) | You're configuring `preToolUse` gates or reading the audit trail |
| [Skills](skills.md) | You want to know what the five skills do and when Copilot picks them |
| [Troubleshooting](troubleshooting.md) | **Something broke** — start here |

## Common problems, fast

| Symptom | See |
|:--------|:----|
| `MSB3923` — can't download the Copilot CLI | [Troubleshooting](troubleshooting.md) |
| CodeQL job shows "skipped" | [Troubleshooting](troubleshooting.md) — expected on private repos |
| `Model "..." is not available` | [Troubleshooting](troubleshooting.md) |
| Port 5050/5051 already in use | [Troubleshooting](troubleshooting.md) |
| `MSB1003` — no project or solution found | The solution is at `src/AgentOrchestrator/AgentHQDemo.slnx` |
| A hook doesn't seem to run | [Hooks and governance](hooks-and-governance.md) |

## Related

- [Labs](../labs/) — hands-on exercises
- [Demos](../demos/) — code walkthroughs
- [Root README](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/README.md) — quick start and endpoint reference
- [`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md) — repository rules for AI agents
