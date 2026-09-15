# Embedding the Copilot SDK

This walkthrough explains how the FastAPI app embeds the GitHub Copilot SDK and turns a Copilot session into an application service. You will see how the app starts the SDK client, creates streaming sessions, listens for session events, bridges callbacks into an async generator, and avoids stale model catalogues.

## Where the SDK is used

The main integration point is [`app/services/copilot_chat.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/services/copilot_chat.py). It imports the SDK with:

```python
from copilot import CopilotClient, SessionEvent, SessionEventType
```

The PyPI package is `github-copilot-sdk`, pinned to **1.0.13** in [`pyproject.toml`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/pyproject.toml), but the import root is `copilot`:

```python
requires-python = ">=3.11"
dependencies = [
    "github-copilot-sdk==1.0.13",
```

Python 3.11 or later is required.

## One long-lived service

[`app/main.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/main.py) creates one chat service for the FastAPI app lifetime:

```python
app.state.chat_service = CopilotChatService()
```

The service connects lazily. `ensure_started()` is guarded by an `asyncio.Lock`, so concurrent HTTP requests cannot race to start two transports:

```python
self._lock = asyncio.Lock()
```

```python
async with self._lock:
    if self._is_started and self._client is not None:
        return
```

Startup and shutdown are explicit:

```python
self._client = CopilotClient()
await self._client.start()
self._is_started = True
```

```python
if self._client is not None:
    await self._client.stop()
```

The SDK also supports `async with CopilotClient()` for short-lived scripts; the lab samples use that form when the client only lives for one command.

## Creating a streaming session

`CopilotChatService.chat_stream()` creates a new SDK session for each prompt:

```python
session = await self._client.create_session(
    model=model,
    streaming=True,
    system_message=(
        {"mode": "append", "content": system_message} if system_message else None
    ),
)
```

`create_session(...)` is keyword-only. The important parameters are `model`, `streaming`, `system_message`, `tools`, `mcp_servers`, `session_id`, and `on_permission_request`.

`system_message` is a TypedDict union:

```python
{"mode": "append", "content": "..."}
{"mode": "replace", "content": "..."}
```

The demo uses append mode so application context is added without replacing the base Copilot behaviour.

⚠️ Python custom tools require `on_permission_request` or the call is denied. The .NET sample needs no handler for the same simple tool flow. Python also needs no `GHCP001` experimental-API suppression to use permission decisions from `copilot.rpc`.

## Session events

The central architectural point: Python SDK events are **push-only callbacks**. `session.on(handler)` registers a handler and returns an unsubscribe callable; there is no async iterator.

Unlike the C# SDK, Python exposes **one** `SessionEvent` dataclass. You branch on `evt.type`, a `SessionEventType` enum, instead of pattern-matching one subclass per event. This is the real handler:

```python
def on_event(evt: SessionEvent) -> None:
    # Unlike .NET, every event arrives as one SessionEvent
    # carrying a `type` enum and a `data` payload, so this
    # dispatches on `evt.type` rather than on subclasses.
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

`ASSISTANT_MESSAGE_DELTA` carries streamed text, `ASSISTANT_MESSAGE` marks the complete answer, `SESSION_IDLE` completes the turn wait, and `SESSION_ERROR` surfaces a failure.

## Bridging callbacks to an async generator

FastAPI streaming wants an async iterator, but the SDK calls a handler. The service bridges callbacks into an `asyncio.Queue` and drains it from `chat_stream()`:

```python
queue: asyncio.Queue[object] = asyncio.Queue()
loop = asyncio.get_running_loop()
```

```python
item = await queue.get()
if item is _DONE:
    break
if isinstance(item, BaseException):
    raise item
yield item  # type: ignore[misc]
```

`_DONE` is a sentinel object, not a content value:

```python
# Sentinel pushed onto the queue when the session goes idle.
_DONE = object()
```

```python
queue.put_nowait(_DONE)
```

A queue is needed because the event callback cannot `yield` to the HTTP response. This is the direct analogue of the C# service writing into a `System.Threading.Channels` channel.

## Reconnect behaviour

If the SDK transport is lost, the service marks the client unhealthy and passes the exception through the queue:

```python
except (ConnectionError, OSError) as ex:
    logger.warning("Copilot connection lost: %s", ex)
    self._is_started = False
    queue.put_nowait(ex)
```

The current request fails, and the next request calls `ensure_started()` again. Since `_is_started` is false, the old client is stopped and a fresh transport is established.

## Listing live models

`CopilotChatService.list_models()` asks the connected Copilot CLI what the signed-in account can use:

```python
models = await self._client.list_models()
return [(m.id, m.name or m.id) for m in models or [] if m.id]
```

The SDK returns `ModelInfo` objects with `.id` and `.name`. Hardcoding model IDs is a trap because availability changes by account and rollout.

There is one known upstream bug: `list_models()` can raise `ValueError: Missing required field 'multiplier' in ModelBilling` ([github/copilot-sdk#1302](https://github.com/github/copilot-sdk/issues/1302)). [`app/routers/chat.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/routers/chat.py) catches broadly and falls back to a static catalogue of six models:

```python
except Exception:
    # Includes the known SDK issue where ModelBilling is missing the
    # required 'multiplier' field: github/copilot-sdk#1302.
    logger.warning(
        "Could not list models from Copilot CLI; using static catalog", exc_info=True
    )

return list(AVAILABLE_MODELS.values())
```

That keeps the model picker usable even when live discovery is temporarily broken.

## Related

- [Streaming responses over SSE](./02-sse-streaming.md)
- [The retail domain](./03-retail-analytics.md)
- [The web UI](./04-web-ui.md)
- Source: [`copilot_chat.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/services/copilot_chat.py), [`chat.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/routers/chat.py), [`main.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/main.py), [`events_sample.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/events_sample.py), [`tools_sample.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/tools_sample.py), [`pyproject.toml`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/pyproject.toml)
