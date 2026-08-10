# Lab 04 — Events

**Goal:** understand the Copilot SDK session event lifecycle: what the SDK
actually emits, the order events arrive in, and which events matter for common
jobs such as streaming, telemetry, completion, and errors.

**Time:** ~20 minutes

**Prerequisites:** [Lab 03](../03-tools/) complete.

## Step 1 — Run the event sample

From the repository root, run the SDK lab sample:

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- events
```

Expected output:

```
== Lab 04: events ==

Prompt: Name two retail KPIs. One line each.

  1. SessionStartEvent
  2. SessionManagedSettingsResolvedEvent
  3. PendingMessagesModifiedEvent
  4. SessionSkillsLoadedEvent
  5. SystemMessageEvent
  6. SessionToolsUpdatedEvent
  7. UserMessageEvent
  8. HookStartEvent
  9. SessionTitleChangedEvent
 10. HookEndEvent
 11. AssistantTurnStartEvent
 12. SessionUsageInfoEvent
 13. ModelCallStartEvent
 14. AssistantStreamingDeltaEvent
 15. AssistantReasoningDeltaEvent
 16. AssistantStreamingDeltaEvent
 17. AssistantReasoningDeltaEvent
 18. AssistantStreamingDeltaEvent
 19. AssistantReasoningDeltaEvent
 20. AssistantStreamingDeltaEvent
 21. AssistantReasoningDeltaEvent
 22. AssistantStreamingDeltaEvent
 23. AssistantMessageStartEvent
 24. AssistantStreamingDeltaEvent
 25. AssistantUsageEvent
 26. AssistantMessageEvent
     content: **Customer Acquisition Cost (CAC):** Total marketing spend divided by number of …
     (preceded by 2 delta events)
 27. AssistantReasoningEvent
 28. AssistantTurnEndEvent
 29. HookStartEvent
 30. HookEndEvent
 31. SessionUsageCheckpointEvent
 32. AssistantIdleEvent
 33. SessionIdleEvent

Total delta events: 2
```

The important surprise is the volume. A simple one-prompt exchange emits far
more than "user message, assistant message, done".

⚠️ The sample counts `AssistantMessageDeltaEvent` separately and suppresses
printing those events. That is why `Total delta events: 2` appears even though
many `AssistantStreamingDeltaEvent` entries are visible in the event list.

## Step 2 — Walk the lifecycle phases

The event stream is easier to remember if you group it by phase:

1. **Session setup (1–6)** — the session starts, managed settings resolve,
   pending messages and skills load, the system message appears, and tools are
   announced with `SessionToolsUpdatedEvent`
2. **User turn (7–10)** — the user message is accepted, hook events run
   in-band, and the session title can change
3. **Assistant turn start (11–13)** — the assistant turn starts, usage
   information surfaces, and the model call begins
4. **Streaming (14–24)** — streaming and reasoning deltas arrive, followed by
   `AssistantMessageStartEvent`
5. **Completion (25–28)** — assistant usage is reported, the final assistant
   message arrives, reasoning is finalised, and the assistant turn ends
6. **Teardown and idle (29–33)** — another hook pair runs, usage is
   checkpointed, the assistant becomes idle, then the whole session becomes
   idle

Hook events are part of the same ordered stream. If you are exploring
governance hooks, that ordering matters because `HookStartEvent` and
`HookEndEvent` appear around the work rather than in a separate side channel.
See [Extra — Governance hooks](../extra-governance-hooks/) and
[hooks and governance](../../breakouts/hooks-and-governance.md) for the
related demo material.

Usage also has its own events: `SessionUsageInfoEvent`,
`AssistantUsageEvent`, and `SessionUsageCheckpointEvent`. Those are the events
to inspect when you want token and cost telemetry rather than text content.

## Step 3 — Subscribe with the v1 pattern

Open
[`EventsSample.cs`](../../../src/AgentOrchestrator/samples/SdkLabs/EventsSample.cs)
and find the subscription:

```csharp
session.On<SessionEvent>(evt =>
{
    Console.WriteLine(evt.GetType().Name);
});
```

⚠️ **The explicit `<SessionEvent>` matters.** With GitHub Copilot SDK v1.x,
the non-generic form no longer infers the type argument:

```csharp
session.On(evt => { });
```

That older v0.x shape fails with `CS0411` in v1.x. If you copy an old sample
and see that compiler error, add the explicit type argument.

Also check the namespace when moving older code forward. SDK v1.0.0 moved from
`GitHub.Copilot.SDK` to:

```csharp
using GitHub.Copilot;
```

## Step 4 — Compare with what the app handles

The sample logs almost everything so you can learn the lifecycle. The real app
does not need all of that.

Open
[`CopilotChatService.cs`](../../../src/AgentOrchestrator/AgentHQDemo.Api/Services/CopilotChatService.cs)
and look at the event switch. It handles only four event types:

```csharp
case AssistantMessageDeltaEvent delta:
    outputChannel.Writer.TryWrite(delta.Data.DeltaContent ?? "");
    break;
case AssistantMessageEvent msg:
    _logger.LogInformation("Assistant response complete: {Length} chars",
        msg.Data.Content?.Length ?? 0);
    break;
case SessionIdleEvent:
    done.SetResult();
    break;
case SessionErrorEvent error:
    done.SetException(new Exception(error.Data.Message));
    break;
```

That is a reasonable production choice. For browser streaming, the app needs
text chunks, final-message logging, a completion signal, and an error path. It
does not need to switch on every setup, hook, reasoning, or telemetry event.

⚠️ There are two different delta event families:
`AssistantStreamingDeltaEvent` and `AssistantMessageDeltaEvent`. In this run,
many `AssistantStreamingDeltaEvent` entries appeared, while the sample counted
only two `AssistantMessageDeltaEvent` values. If you subscribe to the wrong one
for your job, you may see far fewer chunks than expected. Prefer measuring what
your scenario actually emits before assuming the names mean the same thing.

## Step 5 — Complete on idle, fail on error

`SessionIdleEvent` is the completion signal used by the samples and the app.
The usual pattern is a `TaskCompletionSource` that is resolved when the session
becomes idle:

```csharp
var done = new TaskCompletionSource();

session.On<SessionEvent>(evt =>
{
    switch (evt)
    {
        case SessionIdleEvent:
            done.TrySetResult();
            break;
        case SessionErrorEvent error:
            done.TrySetException(new Exception(error.Data.Message));
            break;
    }
});

await session.SendAsync(new MessageOptions { Prompt = prompt });
await done.Task;
```

⚠️ Always handle `SessionErrorEvent`. If the session fails and you never set
the exception on the `TaskCompletionSource`, `await done.Task` can wait
forever. That failure mode is easy to miss because the happy path works
perfectly.

## ⚠️ Traps

- `session.On(evt => ...)` is the old shape; use
  `session.On<SessionEvent>(evt => ...)` with SDK v1.x
- Old namespaces using `GitHub.Copilot.SDK` must become `GitHub.Copilot`
- `AssistantStreamingDeltaEvent` and `AssistantMessageDeltaEvent` are distinct
  event types; do not treat them as interchangeable without measuring
- Logging every event is useful for learning but noisy for app code
- Waiting only for an assistant message is not enough; complete the operation
  on `SessionIdleEvent`
- Ignoring `SessionErrorEvent` can leave your caller hanging forever

## 💡 Extra credit

Try one of these small experiments:

1. Change the sample to print only tool-related events, such as event names
   containing `Tool` or hook events that surround tool work
2. Measure time-to-first-token by starting a `Stopwatch` before
   `SendAsync(...)` and stopping it on the first delta event you care about
3. Count usage-related events separately and log where they appear in the
   lifecycle
4. Add a filter that groups events into the six lifecycle phases above instead
   of printing a flat numbered list

## ✅ Checkpoint

You can now explain:

- [x] The ordered session lifecycle emitted by the SDK
- [x] Why `On<SessionEvent>` needs the explicit type argument in v1.x
- [x] Why real apps usually handle a small subset of all emitted events
- [x] The difference between observing all events and streaming useful chunks
- [x] Why `SessionIdleEvent` completes the operation
- [x] Why `SessionErrorEvent` must fail the waiting task
- [x] Where hook and usage events appear in the lifecycle

## Related

- Previous: [Lab 03 — Tools](../03-tools/)
- Next: [Lab 05 — Sessions](../05-sessions/)
- [Demo: Copilot SDK integration](../../demos/01-copilot-sdk-integration.md)
- [Hooks and governance](../../breakouts/hooks-and-governance.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
