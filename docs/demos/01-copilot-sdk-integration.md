# Embedding the Copilot SDK

This walkthrough explains how the API embeds the GitHub Copilot SDK and turns a
Copilot session into an application service. You will see how the app starts the
SDK client, creates streaming sessions, listens for session events, and avoids
stale model catalogues.

## Where the SDK is used

The main integration point is
[`CopilotChatService`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Services/CopilotChatService.cs).
It imports the SDK with:

```csharp
using GitHub.Copilot;
```

That namespace is a common migration gotcha. Before the 1.0.0 SDK release the
namespace was `GitHub.Copilot.SDK`; in the v1.x code used here, the namespace is
`GitHub.Copilot` even though the package reference is still named
`GitHub.Copilot.SDK`.

## One long-lived client

[`Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Program.cs)
registers `CopilotChatService` as a singleton:

```csharp
builder.Services.AddSingleton<CopilotChatService>();
```

The service owns a single `CopilotClient` field for the lifetime of the API
process. That avoids starting and stopping the Copilot connection for every HTTP
request, and keeps model listing and chat streaming behind one managed service.

`CopilotChatService` implements `IAsyncDisposable`. When the singleton is
disposed by the host, `CopilotChatService.DisposeAsync` calls
`CopilotClient.StopAsync()` so the SDK connection is shut down cleanly.

## Starting and recovering the client

`CopilotChatService.EnsureStartedAsync` is the gate before model listing or chat:

```csharp
if (_isStarted && _client != null) return;

_isStarted = false;
if (_client != null)
{
    try { await _client.StopAsync(); } catch { }
}

_client = new CopilotClient();
await _client.StartAsync();
_isStarted = true;
```

The method treats `_isStarted` and `_client` as the source of truth. If the
client was not started, or a previous failure marked it unhealthy, the service
stops any old client, creates a fresh `CopilotClient`, starts it, and records the
new state.

Recovery is completed in `CopilotChatService.ChatStreamAsync`. The background
session catches `IOException`, logs that the Copilot connection was lost, sets
`_isStarted = false`, and completes the output channel with the exception. The
next request will call `EnsureStartedAsync` again and recreate the client.

## Creating a streaming session

`CopilotChatService.ChatStreamAsync` builds a `SessionConfig` for each prompt:

```csharp
SessionConfig config = new()
{
    Model = model,
    Streaming = true,
    SystemMessage = systemMessage != null ? new SystemMessageConfig
    {
        Mode = SystemMessageMode.Append,
        Content = systemMessage
    } : null
};
```

The selected model comes from the request, with `claude-haiku-4.5` as the
service default. `Streaming = true` asks the SDK to emit response deltas. When a
system message is supplied, it is sent as a `SystemMessageConfig` in `Append`
mode, so the application context is appended instead of replacing the session's
base system behaviour.

The session is created with `await using`, so it is disposed asynchronously after
that prompt finishes:

```csharp
await using var session = await _client.CreateSessionAsync(config);
```

## Session events

The service subscribes to SDK events with an explicit generic type argument:

```csharp
session.On<SessionEvent>(evt => { /* switch on event type */ });
```

In v1.x the explicit `<SessionEvent>` is required. The older non-generic form no
longer infers the event type reliably, so omitting the type argument is a common
upgrade failure.

`CopilotChatService` handles four event shapes:

- `AssistantMessageDeltaEvent` writes `DeltaContent` to the output stream.
- `AssistantMessageEvent` logs that the assistant response is complete.
- `SessionIdleEvent` completes the `TaskCompletionSource` used to wait for the
  turn to finish.
- `SessionErrorEvent` logs the SDK error and completes the wait task with an
  exception.

After subscribing, the prompt is sent with:

```csharp
await session.SendAsync(new MessageOptions { Prompt = prompt });
```

## Bridging SDK events to `IAsyncEnumerable`

The SDK session runs inside a background `Task`. Deltas are written into a
`Channel<string>`, and the outer async iterator reads the channel:

```csharp
var outputChannel = Channel.CreateUnbounded<string>();

_ = Task.Run(async () =>
{
    // create session, handle events, write chunks
}, cancellationToken);

await foreach (var chunk in outputChannel.Reader.ReadAllAsync(cancellationToken))
{
    yield return chunk;
}
```

This shape is intentional. C# async iterators cannot `yield return` from inside
the `try`/`catch` block that owns the SDK session. The channel lets the session
handle exceptions and completion internally, while the outer method exposes a
clean `IAsyncEnumerable<string>` to the controller.

## Listing live models

`CopilotChatService.ListModelsAsync` calls `CopilotClient.ListModelsAsync()` and
returns the IDs and display names actually offered by the connected Copilot CLI:

```csharp
var models = await _client.ListModelsAsync(cancellationToken);
```

Hardcoding model IDs is a trap. Model availability changes by account, rollout,
and provider; a stale hardcoded list in this demo previously left only one of six
models working. The API still has a static catalogue in
[`ChatController.AvailableModels`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/ChatController.cs),
but it is used as metadata and fallback. The normal path is to fetch the live
model list from the SDK.

## Related

- [Streaming responses over SSE](./02-sse-streaming.md)
- [The retail domain](./03-retail-analytics.md)
- [The Blazor front end](./04-blazor-ui.md)
- Source:
  [`CopilotChatService.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Services/CopilotChatService.cs),
  [`Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Program.cs),
  [`ChatController.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/ChatController.cs)
