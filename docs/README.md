# /docs

Documentation and step-by-step content for the session, organised into three
sections.

## 📚 Sections

| Section | What it is | Start here |
|:--------|:-----------|:-----------|
| [**Labs**](labs/) | Hands-on Copilot SDK exercises, ~2 hours end to end | [Lab 01 — Setup](labs/01-setup/) |
| [**Demos**](demos/) | Walkthroughs explaining the code in [`/src`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator) | [Copilot SDK integration](demos/01-copilot-sdk-integration.md) |
| [**Breakouts**](breakouts/) | Diagrams, configuration reference, troubleshooting | [Architecture](breakouts/architecture.md) |

### Labs — the SDK path

| # | Lab | Time |
|:--|:----|:-----|
| 01 | [Setup](labs/01-setup/) — build, run, verify | ~15 min |
| 02 | [First chat](labs/02-first-chat/) — SSE streaming and runtime models | ~20 min |
| 03 | [Tools](labs/03-tools/) — `CopilotTool.DefineTool` | ~20 min |
| 04 | [Events](labs/04-events/) — the session event lifecycle | ~20 min |
| 05 | [Sessions](labs/05-sessions/) — persistence and resume | ~20 min |
| 06 | [MCP](labs/06-mcp/) — attach an MCP server | ~20 min |
| 07 | [Wrap-up](labs/07-wrap-up/) — consolidate and clean up | ~10 min |

Backed by a runnable samples project at
[`src/AgentOrchestrator/samples/SdkLabs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator/samples/SdkLabs).

### Labs — extras (not the SDK)

Optional. These cover Copilot **CLI** and general app development.

[Custom agents](labs/extra-custom-agents/) ·
[Governance hooks](labs/extra-governance-hooks/) ·
[Extend the API](labs/extra-extend-api/)

### Demos

| # | Walkthrough |
|:--|:------------|
| 01 | [Copilot SDK integration](demos/01-copilot-sdk-integration.md) |
| 02 | [SSE streaming](demos/02-sse-streaming.md) |
| 03 | [Retail analytics](demos/03-retail-analytics.md) |
| 04 | [Blazor UI](demos/04-blazor-ui.md) |
| — | [Demo script](demos/demo-script.md) — presenter talk track |

### Breakouts

[Architecture](breakouts/architecture.md) ·
[Custom agents](breakouts/custom-agents.md) ·
[Hooks and governance](breakouts/hooks-and-governance.md) ·
[Skills](breakouts/skills.md) ·
[Troubleshooting](breakouts/troubleshooting.md)

## 🖼️ Assets

| Path | Purpose |
|:-----|:--------|
| `Slide1.png` – `Slide3.png` | Session slides — the "Three Mondays" narrative |
| `screenshots/` | UI screenshots referenced from the README |

## Adding content

- **Labs** — one folder per exercise, numbered: `07-your-topic/README.md`
- **Demos** — one file per area of the codebase, numbered
- **Breakouts** — one file per reference topic, named not numbered
- Keep images in `screenshots/` or an `assets/` subfolder
- Update the tables above and the relevant section `README.md`

## Content rules

- **No large binaries** — no PowerPoint decks, videos, or recordings. Link to
  them instead.
- **Nothing confidential** — no customer names, competitive analysis, account
  plans, or internal-only material. See [`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md).
- **Don't "fix" the intentional code smells** — four flawed patterns exist
  deliberately for review demonstrations.

## Licensing

Documentation in this folder is licensed under
[CC BY 4.0](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/LICENSE-DOCS). Source code in `/src` is licensed separately
under the [MIT License](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/LICENSE).
