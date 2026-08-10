# Lab 05 — Sessions

**Goal:** persist and resume a Copilot SDK conversation, the building block
behind "your agent, anywhere".

**Time:** ~20 minutes

**Prerequisites:** [Lab 04](../04-events/) complete.

## Step 1 — Understand why persistence matters

Without session persistence, every process restart is amnesia. The assistant can
only see the messages you send in the current process, so a CLI crash, browser
refresh or server recycle loses the conversation.

With a stored session, the conversation has an identity. You can start it in a
CLI, resume it from a web app, then pick it up later from a phone. That is the
core pattern behind "your agent, anywhere": the client changes, but the session
history stays attached to the same id.

## Step 2 — Create a session with a known id

Open
[`SessionsSample.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/samples/SdkLabs/SessionsSample.cs)
and find the session id:

```csharp
var sessionId = $"sdklabs-{Guid.NewGuid():N}"[..24];
```

Then find where it is passed to the SDK:

```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    SessionId = sessionId,
    Model = "claude-haiku-4.5",
    Streaming = false
});
```

`SessionConfig.SessionId` lets you supply your own id. If you omit it, the SDK
generates one for you, but then your app needs to capture and store that
generated value before it can resume the session later.

For real apps, use ids that are meaningful and unique to your domain, such as a
conversation id or support case id. Do not put secrets in them: ids often appear
in logs, diagnostics and URLs.

## Step 3 — Run the sample

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- sessions
```

Expected output:

```
== Lab 06: sessions ==

Session id: sdklabs-b6f3d805dd4a4767

--- Turn 1 (new session) ---
You: Remember this: my favourite retail segment is 'At Risk'. Reply with just OK.
Assistant: OK

Session disposed.

--- Turn 2 (resumed session) ---
You: Which retail segment did I say was my favourite?
Assistant: Your favourite retail segment is 'At Risk'.

--- Session metadata ---
  id=sdklabs-b6f3d805dd4a4767 metadata retrieved
```

The banner still says `Lab 06` because the sample was written before the labs
were renumbered. The session id is random per run, so yours will differ.

The important proof is not the id value. It is that turn 2 remembered
`'At Risk'` after the first session had been disposed.

## Step 4 — Resume the session

The second turn uses the same id, but a different SDK call:

```csharp
await using var resumed = await client.ResumeSessionAsync(
    sessionId,
    new ResumeSessionConfig
    {
        Model = "claude-haiku-4.5",
        Streaming = false
    });
```

⚠️ **The config parameter is required.** In SDK v1.0.9 this does not compile:

```csharp
await client.ResumeSessionAsync(sessionId);
```

The compiler reports `CS7036` because there is no argument for the required
configuration parameter.

⚠️ **Use `ResumeSessionConfig`, not `SessionConfig`.** Creating a new session
uses `SessionConfig`; resuming an existing one uses `ResumeSessionConfig`.
The resume config carries the same kind of settings used here, including
`Model` and `Streaming`, but it is a different type.

## Step 5 — Discover stored sessions

The sample also asks the SDK for metadata:

```csharp
var metadata = await client.GetSessionMetadataAsync(sessionId);
```

That call returns metadata for a stored session. To browse available stored
sessions instead of starting from a known id, use `ListSessionsAsync(...)`:

```csharp
var sessions = await client.ListSessionsAsync(...);
```

A common pattern is:

1. List sessions for the signed-in user
2. Let the user choose one, or select the most recent
3. Pass that id to `ResumeSessionAsync(id, config)`

## Step 6 — Connect it to the architecture

Session persistence enables:

- **Hand-off across devices and clients** — start in one place, continue in
  another
- **Crash recovery** — resume after a process restart instead of rebuilding
  context from scratch
- **Auditability** — stable ids make it easier to inspect, organise and trace
  conversations

In the demo app today, the browser keeps chat history in localStorage via
`StorageService`. That works for one browser on one device, but it cannot move
the conversation to another device. If you open the app on a phone or another
machine, the history is gone.

SDK sessions are the fix. The UI can store a session id instead of the whole
conversation, and any client that knows that id can resume the same server-side
session.

## ⚠️ Traps

- **Id collisions:** the session id is your key. Reusing an id resumes the old
  conversation instead of creating a clean one.
- **Opaque ids:** random ids work for demos, but real systems should be able to
  map ids back to users, cases or workflows.
- **Secrets in ids:** never include tokens, email addresses, customer secrets or
  confidential data in a session id.
- **Wrong resume overload:** `ResumeSessionAsync(sessionId)` fails with
  `CS7036`; pass `ResumeSessionConfig`.
- **Wrong config type:** `SessionConfig` is for create; `ResumeSessionConfig`
  is for resume.

## 💡 Extra credit

Resume the same session twice:

1. Add a third turn after the metadata call
2. Resume the same `sessionId` again
3. Ask another question about the original `'At Risk'` message

Or list stored sessions and resume the most recent one:

```csharp
var sessions = await client.ListSessionsAsync(...);
```

Then pass the selected id to:

```csharp
await client.ResumeSessionAsync(id, new ResumeSessionConfig
{
    Model = "claude-haiku-4.5",
    Streaming = false
});
```

## ✅ Checkpoint

You can now explain:

- [x] Why process restarts lose context without persistence
- [x] How `SessionConfig.SessionId` gives your app a stable conversation key
- [x] Why `ResumeSessionAsync(id, config)` requires `ResumeSessionConfig`
- [x] How `GetSessionMetadataAsync` and `ListSessionsAsync(...)` help discover
  stored sessions
- [x] Why SDK sessions are the right foundation for cross-device chat history

## Related

- Previous: [Lab 04 — Events](../04-events/)
- Next: [Lab 06 — MCP](../06-mcp/)
- [Demo: Copilot SDK integration](../../demos/01-copilot-sdk-integration.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
