# Demos

Walkthroughs of the code in [`/src`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator). Read these
to understand *how* the Agent HQ demo works — the [labs](../labs/) are the
hands-on counterpart.

## Walkthroughs

| # | Doc | Covers |
|:--|:----|:-------|
| 01 | [Copilot SDK integration](01-copilot-sdk-integration.md) | `CopilotChatService` — client lifecycle, session events, runtime model discovery |
| 02 | [SSE streaming](02-sse-streaming.md) | `ChatController` → browser: wire format, flushing, error contract |
| 03 | [Retail analytics](03-retail-analytics.md) | Domain models, EF Core, seeding, prediction, and the intentional code smells |
| 04 | [Blazor UI](04-blazor-ui.md) | `Home.razor`, model picker, localStorage, streaming render |

## Presenting

- [**demo-script.md**](demo-script.md) — the capability-focused talk track,
  with timings and a commands cheat sheet

## Suggested order

If you're new to the codebase, read them in numbered order — each assumes the
previous. If you're preparing to present, start with the demo script and dip
into the walkthroughs for the sections you'll be asked about.

## ⚠️ On the intentional code smells

[Walkthrough 03](03-retail-analytics.md) documents four flawed patterns that
exist **on purpose** for code-review demonstrations. They are not bugs to fix.
See [`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md).

## Related

- [Labs](../labs/) — hands-on exercises covering this same ground
- [Breakouts](../breakouts/) — architecture diagrams and reference material
- [Architecture](../breakouts/architecture.md) — the system at a glance
