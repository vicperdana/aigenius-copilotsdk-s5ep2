# Lab 04 — Events

**Goal:** understand the Copilot SDK session event lifecycle: what the SDK
actually emits, the order events arrive in, and which events matter for common
jobs such as streaming, telemetry, completion, and errors.

**Time:** ~20 minutes

**Prerequisites:** [Lab 03](../03-tools/) complete.

## Step 1 — Run the event sample

From `src/AgentOrchestrator-python`, run the SDK lab sample:

```bash
cd src/AgentOrchestrator-python
uv run python -m sdk_labs events
```

Add `--model <id>` to override the model:

```bash
uv run python -m sdk_labs events --model gpt-5-mini
```

Expected output:

```text
== Lab 04: events ==

Model: claude-haiku-4.5
Prompt: Name two retail KPIs. One line each.

  1. session.start
  2. pending_messages.modified
  3. session.skills_loaded
  4. system.message
  5. session.tools_updated
  6. user.message
  7. hook.start
  8. session.title_changed
  9. hook.end
 10. assistant.turn_start
 11. session.usage_info
 12. model.call_start
 13. assistant.streaming_delta
 14. assistant.reasoning_delta
 15. assistant.streaming_delta
 16. assistant.reasoning_delta
 17. assistant.streaming_delta
 18. assistant.reasoning_delta
 19. assistant.streaming_delta
 20. assistant.reasoning_delta
 21. assistant.streaming_delta
 22. assistant.reasoning_delta
 23. assistant.streaming_delta
 24. assistant.reasoning_delta
 25. assistant.streaming_delta
 26. assistant.message_start
 27. assistant.streaming_delta
 28. assistant.streaming_delta
 29. assistant.usage
 30. assistant.message
     content: 1. **Sales per Square Foot** — Revenue generated per unit of retail floor space;…
     (preceded by 3 delta events)
 31. assistant.reasoning
 32. assistant.turn_end
 33. hook.start
 34. hook.end
 35. session.usage_checkpoint
 36. assistant.idle
 37. session.idle

Total delta events: 3
 38. session.shutdown
 39. session.background_tasks_changed
 40. session.background_tasks_changed
```

The important surprise is the volume. A simple one-prompt exchange emits far
more than "user message, assistant message, done".

⚠️ **Events 38–40 arrive after the summary line.** `Total delta events: 3` is
printed as soon as the session reports idle, but the `async with session:` block
has not exited yet. Teardown emits three more events on the way out. That is a
useful reminder that *idle is not the same as closed*.

⚠️ **The numbers above are one observed run, not a contract.** Exact counts and
ordering vary by model, prompt, and SDK version — read the sequence for its
shape, not as a fixed specification.

## Step 2 — Walk the lifecycle phases

The event stream is easier to remember if you group it by phase:

1. **Session setup (1–5)** — the session starts, pending messages and skills
   load, the system message appears, and tools are announced with
   `session.tools_updated`
2. **User turn (6–9)** — the user message is accepted, hook events run in-band,
   and the session title can change
3. **Assistant turn start (10–12)** — the assistant turn starts, usage
   information surfaces, and the model call begins
4. **Streaming (13–28)** — streaming and reasoning deltas arrive, followed by
   `assistant.message_start`
5. **Completion (29–32)** — assistant usage is reported, the final assistant
   message arrives, reasoning is finalised, and the assistant turn ends
6. **Teardown and idle (33–37)** — another hook pair runs, usage is
   checkpointed, the assistant becomes idle, then the whole session becomes idle
7. **Shutdown (38–40)** — emitted while `async with session:` unwinds

Hook events are part of the same ordered stream. If you are exploring
governance hooks, that ordering matters because `hook.start` and `hook.end`
appear around the work rather than in a separate side channel. See
[Extra — Governance hooks](../../labs/extra-governance-hooks/) and
[hooks and governance](../../breakouts/hooks-and-governance.md).

Usage also has its own events: `session.usage_info`, `assistant.usage`, and
`session.usage_checkpoint`. Those are the events to inspect when you want token
and cost telemetry rather than text content.

## Step 3 — Subscribe with `session.on`

Open
[`events_sample.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/events_sample.py)
and find the subscription:

```python
            waiter = IdleWaiter()
            counters = {"order": 0, "deltas": 0}

            def on_event(evt: SessionEvent) -> None:
                # Deltas arrive in a flood; count them instead of printing each one.
                if evt.type is SessionEventType.ASSISTANT_MESSAGE_DELTA:
                    counters["deltas"] += 1
                    return

                counters["order"] += 1
                # Unlike C#, the event type is a value on the event rather than
                # a subclass, so this prints evt.type instead of a class name.
                print(f"{counters['order']:3d}. {evt.type.value}")

                if evt.type is SessionEventType.ASSISTANT_MESSAGE:
                    print(f"     content: {trim(evt.data.content)}")
                    print(f"     (preceded by {counters['deltas']} delta events)")
                elif evt.type is SessionEventType.SESSION_ERROR:
                    print(f"     ERROR: {evt.data.message}")

                waiter.handle(evt)

            session.on(on_event)
```

The lab project pins PyPI `github-copilot-sdk` **1.0.13**, imports it from
`copilot`, and requires Python 3.11 or later.

⚠️ **This is the biggest structural difference from the .NET SDK.** In C# every
event is its own class and you pattern-match on the subclass:

```csharp
// .NET — one class per event
session.On<SessionEvent>(evt => Console.WriteLine(evt.GetType().Name));
```

In Python there is exactly **one** `SessionEvent` dataclass. The kind of event
is a *value* on the object, not its type.

That is why the transcript prints `assistant.message` (the enum's `.value`)
where the .NET lab printed `AssistantMessageEvent` (the class name). Use `is`
for the comparison — `SessionEventType` members are singletons.

`SessionEvent` carries `data`, `id`, `timestamp`, `type`, `agent_id`,
`ephemeral`, `parent_id`, and `raw_type`. The shape of `evt.data` depends on
`evt.type`, which is why the sample only reads `evt.data.content` inside the
`ASSISTANT_MESSAGE` branch.

💡 `session.on(handler)` **returns an unsubscribe callable**. Keep it if you
need to stop listening before the session ends; for example, store the result
of `session.on(on_event)` and call it later.

## Step 4 — Compare with what the app handles

The sample logs almost everything so you can learn the lifecycle. The real app
does not need all of that.

Open
[`copilot_chat.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/services/copilot_chat.py)
and look at its handler. It reacts to only four event types:

```python
                        if evt.type is SessionEventType.ASSISTANT_MESSAGE_DELTA:
                            queue.put_nowait(evt.data.delta_content or "")
                        elif evt.type is SessionEventType.ASSISTANT_MESSAGE:
                            logger.info(
                                "Assistant response complete: %d chars",
                                len(evt.data.content or ""),
                            )
                        elif evt.type is SessionEventType.SESSION_IDLE:
                            if not done.done():
                                done.set_result(None)
                        elif evt.type is SessionEventType.SESSION_ERROR:
                            logger.error("Session error: %s", evt.data.message)
                            if not done.done():
                                done.set_exception(RuntimeError(evt.data.message))
```

That is a reasonable production choice. For browser streaming, the app needs
text chunks, final-message logging, a completion signal, and an error path. It
does not need to branch on every setup, hook, reasoning, or telemetry event.

Notice the two mechanisms working together: text chunks go onto an
`asyncio.Queue` so they can be streamed out immediately, while idle and error
resolve a `Future` that tells the generator when to stop. That split is the
subject of [Demo 01](../../demos-python/01-copilot-sdk-integration.md).

⚠️ **There are two different delta families.**
`assistant.streaming_delta` and `assistant.message_delta` are *not* the same
event. The sample suppresses only `ASSISTANT_MESSAGE_DELTA`, which is why
`Total delta events: 3` coexists with many visible `assistant.streaming_delta`
lines in the transcript above. If you subscribe to the wrong one you may see far
fewer chunks than expected. Measure what your scenario actually emits before
assuming the names mean the same thing.

## Step 5 — Complete on idle, fail on error

`session.idle` is the completion signal used by the samples and the app.
Because events are **push-only callbacks** — there is no async iterator to
`await` — you need a future that the callback resolves. The samples share
[`IdleWaiter`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/_common.py):

```python
class IdleWaiter:
    """Resolves when the session reports idle, or raises on session error."""

    def __init__(self) -> None:
        self._future: asyncio.Future[None] = asyncio.get_event_loop().create_future()

    def handle(self, evt: SessionEvent) -> bool:
        """Returns True when the event was a terminal (idle/error) event."""
        if evt.type is SessionEventType.SESSION_IDLE:
            if not self._future.done():
                self._future.set_result(None)
            return True
        if evt.type is SessionEventType.SESSION_ERROR:
            if not self._future.done():
                self._future.set_exception(RuntimeError(evt.data.message))
            return True
        return False

    async def wait(self) -> None:
        await asyncio.wait_for(self._future, timeout=TIMEOUT_SECONDS)
```

In the event sample, the callback above calls `waiter.handle(evt)` for each
event; after sending the prompt, the sample awaits `waiter.wait()` before
printing the summary.

This is the Python analogue of C#'s `TaskCompletionSource`.

⚠️ **Always handle the error event.** If the session fails and nothing sets the
exception, `await waiter.wait()` waits until the timeout expires. That failure
mode is easy to miss because the happy path works perfectly.

⚠️ **Always set a timeout.** `IdleWaiter.wait()` wraps the future in
`asyncio.wait_for(..., timeout=180)`. A dropped transport means idle *and*
error may never arrive, and without the timeout your coroutine hangs forever.

💡 If you only want the reply and do not care about the lifecycle, the SDK has
a shortcut that does this waiting for you:
`await session.send_and_wait(prompt, timeout=180)`.

## ⚠️ Traps

- There is one `SessionEvent` dataclass; branch on `evt.type`, not on subclasses
- `assistant.streaming_delta` and `assistant.message_delta` are distinct events
- Idle is not closed — more events arrive as the `async with` block unwinds
- Logging every event is useful for learning but noisy for app code
- Waiting only for an assistant message is not enough; complete on `session.idle`
- Ignoring `session.error` leaves your caller waiting for the full timeout
- Handlers are called *by the SDK* — keep them fast and push work onto a queue
  rather than doing slow work inline

## 💡 Extra credit

1. Change the sample to print only tool-related events — those whose
   `evt.type.value` starts with `tool.`
2. Measure time-to-first-token by recording `time.perf_counter()` before
   `session.send(...)` and stopping on the first delta you care about
3. Count usage-related events separately and log where they appear
4. Group events into the seven lifecycle phases above instead of printing a flat
   numbered list
5. Print `evt.raw_type` alongside `evt.type.value` and see where the two differ

## ✅ Checkpoint

You can now explain:

- [x] The ordered session lifecycle emitted by the SDK
- [x] Why Python uses one `SessionEvent` with an `evt.type` enum, unlike C#
- [x] That `session.on` returns an unsubscribe callable
- [x] Why real apps handle a small subset of all emitted events
- [x] The difference between `streaming_delta` and `message_delta`
- [x] Why `session.idle` completes the operation, and why idle ≠ closed
- [x] Why the error event must fail the waiting future, and why timeouts matter
- [x] Where hook and usage events appear in the lifecycle

## Related

- Previous: [Lab 03 — Tools](../03-tools/)
- Next: [Lab 05 — Sessions](../05-sessions/)
- [Demo: Copilot SDK integration](../../demos-python/01-copilot-sdk-integration.md)
- [Extra — Governance hooks](../../labs/extra-governance-hooks/)
- [Hooks and governance](../../breakouts/hooks-and-governance.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
