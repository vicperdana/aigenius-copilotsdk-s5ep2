# Lab 02 — Your first streaming chat

**Goal:** follow a single prompt all the way through the stack — browser →
API → Copilot SDK → model → back — and understand why the model list is
fetched at runtime rather than hardcoded.

**Time:** ~20 minutes

**Prerequisites:** [Lab 01](../01-setup/) complete, both services running.

## Step 1 — Watch the wire format

Send a prompt and observe the raw Server-Sent Events:

```bash
curl -N -X POST http://localhost:5050/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"prompt":"Name three retail KPIs. One line each.","model":"claude-haiku-4.5"}'
```

You'll see many small frames rather than one big response:

```
data: {"content":"1. **Average"}

data: {"content":" Transaction Value** — revenue divided by"}

data: {"content":" transaction count.\n"}

...

data: [DONE]
```

Three things to notice:

1. Each frame is `data: ` followed by JSON, then a **blank line** — that blank
   line is what SSE uses to delimit events
2. Chunks split at arbitrary points, mid-word and mid-sentence. The client must
   concatenate; it can't assume whole tokens
3. The stream ends with the sentinel `data: [DONE]`

## Step 2 — Find the server side

Open [`ChatController.cs`](../../../src/AgentOrchestrator/AgentHQDemo.Api/Controllers/ChatController.cs)
and locate `StreamChat`. Note in order:

- `Response.ContentType = "text/event-stream"` plus `no-cache` and keep-alive
- The `await foreach` over `_chatService.ChatStreamAsync(...)`
- `await Response.Body.FlushAsync(cancellationToken)` after **every** chunk
- The trailing `data: [DONE]`

⚠️ **The flush is not optional.** Without it ASP.NET Core buffers the response
and the client receives everything at once — the stream still "works" but the
typing effect disappears entirely. This is the single most common mistake when
building SSE endpoints.

Now look at the `catch` block. Errors are written **into the stream** as
`data: {"error":"..."}` rather than returned as an HTTP 500. That's forced on
us: the status line and headers were already sent with the first chunk, so
there is no status code left to change.

## Step 3 — Find the SDK integration

Open [`CopilotChatService.cs`](../../../src/AgentOrchestrator/AgentHQDemo.Api/Services/CopilotChatService.cs).

`ChatStreamAsync` creates a session and subscribes to events:

```csharp
session.On<SessionEvent>(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            outputChannel.Writer.TryWrite(delta.Data.DeltaContent ?? "");
            break;
        case SessionIdleEvent:
            done.SetResult();
            break;
        ...
    }
});
```

⚠️ **The explicit `<SessionEvent>` matters.** In SDK v1.x the type argument no
longer infers from the lambda — `session.On(evt => ...)` fails to compile with
`CS0411`. Older samples written against v0.x still show the non-generic form.

Note also the `Channel<string>`: the SDK session runs inside a background
`Task`, and completed chunks are pushed onto a channel that the enumerator
reads from. That indirection exists because C# forbids `yield return` inside a
`try`/`catch`, and the session work genuinely needs exception handling.

## Step 4 — Ask the API which models you have

```bash
curl -s http://localhost:5050/api/chat/models | jq -r '.[].id'
```

The list comes from **your account**, live. Now try one that almost certainly
isn't on it:

```bash
curl -N -X POST http://localhost:5050/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"prompt":"hello","model":"gpt-4-turbo-preview"}'
```

Expected:

```
data: {"error":"... Model \"gpt-4-turbo-preview\" is not available."}
```

This is exactly why `ChatController.GetModels` calls
`CopilotChatService.ListModelsAsync()` instead of returning a fixed list. An
earlier version of this demo shipped six hardcoded model ids; over time five of
them stopped being valid, and the picker silently offered models that failed on
use. The static catalogue now exists only as a fallback for when the CLI can't
be reached.

## Step 5 — Switch models and compare

Pick two ids from your live list and ask the same question:

```bash
MODEL=$(curl -s http://localhost:5050/api/chat/models | jq -r '.[0].id')
echo "Using $MODEL"

curl -N -X POST http://localhost:5050/api/chat/stream \
  -H "Content-Type: application/json" \
  -d "{\"prompt\":\"In one sentence, what is customer churn?\",\"model\":\"$MODEL\"}"
```

Repeat with a different id and compare latency and tone. In the browser, the
**Model** dropdown does the same thing — the selection is persisted to
localStorage by `StorageService`.

💡 If a model saved in your browser later disappears from your account,
`Home.razor` detects the stale value on load and falls back to a valid one
rather than failing on first send.

## Step 6 — Shape the response with a system message

The API accepts an optional `systemMessage`, applied in **append** mode so it
supplements rather than replaces the built-in instructions:

```bash
curl -N -X POST http://localhost:5050/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{
    "prompt":"Which segment has the lowest retention?",
    "model":"claude-haiku-4.5",
    "systemMessage":"You are a retail analytics assistant. Context: 4 segments — High Value (92% retention), Regular (78%), At Risk (45%), New (65%). Answer in one sentence."
  }'
```

Expected — a grounded answer naming **At Risk** at 45%.

Without that context the model has no access to your seed data and will say so.
Compare by dropping `systemMessage` and re-running. The Blazor client always
sends a retail-analytics system message, which is why the UI feels
domain-aware — see `ChatService.StreamChatAsync`.

This lab fed context through a **system message**. That is static and burns
tokens on every call. Lab 03 replaces that with a **tool** the model can call
on demand when it actually needs retail data.

## ✅ Checkpoint

You can now explain:

- [x] The SSE wire format and why each chunk is flushed
- [x] Why errors are streamed rather than returned as HTTP status codes
- [x] Why `On<SessionEvent>` needs its explicit type argument
- [x] Why models are discovered at runtime
- [x] How a system message grounds the assistant in the retail domain

## Related

- Next: [Lab 03 — Tools](../03-tools/)
- [Demo: Copilot SDK integration](../../demos/01-copilot-sdk-integration.md)
- [Demo: SSE streaming](../../demos/02-sse-streaming.md)
