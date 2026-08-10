# Lab 07 — Wrap-up

**Goal:** consolidate what you built, tidy your machine, and choose a sensible
next step.

**Time:** ~10 minutes

## What you covered

| Lab | Capability |
|:----|:-----------|
| [01](../01-setup/) | Built the app and smoke-tested the SDK samples project |
| [02](../02-first-chat/) | Traced a streaming chat turn and discovered models at runtime |
| [03](../03-tools/) | Replaced static context with `CopilotTool.DefineTool` tools |
| [04](../04-events/) | Observed the session event lifecycle and completion signals |
| [05](../05-sessions/) | Persisted and resumed SDK sessions across process restarts |
| [06](../06-mcp/) | Connected MCP servers to extend the agent with external tools |

The optional `extra-*` labs now sit outside the main SDK path. Use them when
you want CLI custom agents, governance hooks, or ASP.NET/EF Core extension
practice, but they are not required for the SDK sequence.

## The ideas worth keeping

1. **Embedding beats chatting.** The SDK turns an agent into a component of
   your application, subject to your auth, your logging, and your deployment
   pipeline.

2. **Discover capabilities; don't hardcode them.** Models come from
   `ListModelsAsync()`. This demo once shipped six hardcoded model ids and
   quietly degraded to one working option.

3. **Tools beat context-stuffing.** `CopilotTool.DefineTool` lets the model
   fetch what it needs instead of pre-loading every turn with guesses that burn
   tokens whether they are useful or not.

4. **The event stream is richer than you think.** A single turn emitted 33
   events in testing. Most apps handle four, and that is fine, but know what is
   available before you throw the rest away.

5. **Sessions make the agent portable.** `SessionId` plus
   `ResumeSessionAsync` survives process restarts. The demo app's
   browser-localStorage history is convenient, but it cannot move between
   devices.

6. **MCP extends reach.** MCP gives the agent tools you did not write, over a
   standard protocol, without baking every integration into your app.

7. **Verify permission controls in your environment.** `OnPermissionRequest`
   exists, but in our testing it was never invoked because the host CLI had
   pre-granted approval. Even a reject-everything handler let commands through.
   Before relying on it as a control, prove it fires for your setup. Revisit
   [Lab 03](../03-tools/) for the tool-permission sample.

## Clean up

Stop the services (`Ctrl+C` in each terminal), or if they were detached:

```bash
lsof -ti:5050        # prints a PID if still listening
kill <PID>
lsof -ti:5051
kill <PID>
```

Remove local artefacts:

```bash
rm -f src/AgentOrchestrator/AgentHQDemo.Api/retail.db*   # SQLite DB + WAL files
rm -f logs/*                                             # sample and audit logs
```

⚠️ If a file is still open on macOS, the remove command may appear to succeed
while a service recreates it. Stop the service first, then remove the artefact.

```bash
git status --short
```

Expected: no output, or only the lab files you intentionally edited. If you
want to discard local lab work and return to a clean checkout:

```bash
git status
git checkout -- .        # discards uncommitted changes — irreversible
```

## Check your understanding

1. Why does `session.On(...)` fail to compile without a type argument?
2. Why does `ResumeSessionAsync` need a second argument?
3. Why are `[Description]` attributes on tool parameters important?
4. What signals that a turn is complete?

<details>
<summary>Answers</summary>

1. `CS0411` — the type argument cannot be inferred. SDK v1.x requires
   `On<SessionEvent>(...)`; the namespace also moved from
   `GitHub.Copilot.SDK` to `GitHub.Copilot` in v1.0.0.
2. Resume needs configuration as well as the id. Pass a `ResumeSessionConfig`,
   not `SessionConfig`; omitting it produces `CS7036`.
3. They are the model's only API documentation for the tool. Without clear
   descriptions, the model has to guess what arguments mean and when to use
   them.
4. `SessionIdleEvent` means the turn is complete. `SessionErrorEvent` must set
   the exception path too, otherwise your caller can wait forever.

</details>

## Where to go next

| Direction | Start here |
|:----------|:-----------|
| Re-run a focused SDK sample | [`SdkLabs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator/samples/SdkLabs) |
| Understand the demo code in depth | [Demos](../../demos/) |
| Reference troubleshooting and architecture | [Breakouts](../../breakouts/) |
| Build your own agent app | [Copilot SDK repo](https://github.com/github/copilot-sdk) |
| Extend Copilot with external tools | [Model Context Protocol](https://modelcontextprotocol.io/) |

### Ideas to take further

- **Promote a sample into the app.** Move one `SdkLabs` command into a real API
  endpoint and add user-facing progress.
- **Persist conversations server-side.** Replace browser localStorage with
  SQLite-backed session metadata so history survives across devices.
- **Add a second MCP server.** Keep credentials out of source, document the
  required environment variables, and prove the tools appear at runtime.
- **Tighten observability.** Log the event types you ignore today so production
  debugging has enough context without storing full prompts.

## ✅ Final checkpoint

- [x] All seven SDK labs complete
- [x] Services stopped, local artefacts cleaned up
- [x] `git status --short` is clean, or only intentional lab edits remain
- [x] You can answer the four questions above

## Related

- [Labs index](../README.md)
- [Root README](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/README.md)
- [`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md)
