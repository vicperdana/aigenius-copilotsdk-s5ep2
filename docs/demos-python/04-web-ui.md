# The web UI

This walkthrough explains the static browser client that sits in front of the Python retail analytics API. You will see how FastAPI serves the files, how vanilla JavaScript streams chat responses into the message list, how local settings are persisted, and how the model picker stays aligned with the live model list.

## App shape and port

The Python UI is static HTML and vanilla JavaScript under [`app/static/index.html`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/index.html) and [`app/static/app.js`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/app.js). It is served by FastAPI, not Blazor WebAssembly.

There is no build step and no WebAssembly runtime download. The trade-off is no component model, compile-time UI type safety, or generated client code.

One server on port **5070** serves both the API and the UI. That differs from the .NET track, where the API runs on **5050** and the Blazor UI runs on **5051**. Same-origin browser requests mean there is no CORS hop for the normal UI path.

⚠️ Port choice is not arbitrary. Chrome, Edge, and Firefox block port **5060** (SIP) outright, so a UI served there fails with `ERR_UNSAFE_PORT` even though `curl` succeeds. Avoid 5060, 5061, and 6000 when picking a port for anything a browser must load.

Empty, the page shows the welcome heading and five suggested retail questions:

![The Retail Analytics Assistant UI in its empty state: header with the model dropdown, Clear button and theme toggle, a centred welcome heading, five suggestion chips, and the message input.](../screenshots/python-chat-ui-empty.png)

## FastAPI static mount

[`app/main.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/main.py) includes the API routers first:

```python
app.include_router(chat.router)
app.include_router(transactions.router)
app.include_router(segments.router)
```

Then it mounts the static app at `/`:

```python
# Mounted last so it does not shadow the /api routes above.
app.mount("/", StaticFiles(directory=STATIC_DIR, html=True), name="static")
```

⚠️ The order matters. `StaticFiles` is mounted at `/`, so mounting it before the routers would shadow `/api/...` requests.

## `index.html`

[`index.html`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/index.html) contains the whole page shell: header, model picker, clear button, theme toggle, message container, welcome panel, suggestions, textarea, and send button.

```html
<h1>📊 Retail Analytics Assistant</h1>
<span class="badge">Copilot SDK Demo · Python</span>
```

The page loads `marked` and `highlight.js` from CDNs for markdown rendering and syntax highlighting. The stylesheet is [`app/static/app.css`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/app.css), copied from the Blazor project with Blazor-only rules removed so both tracks look identical.

## Page state and local storage

[`app.js`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/app.js) keeps the same state as the Blazor `Home.razor` page:

```javascript
let messages = [];
let selectedModel = 'claude-haiku-4.5';
let isDark = true;
let isStreaming = false;
```

It persists messages, selected model, and theme with the same keys as the Blazor client:

```javascript
const STORAGE_KEYS = {
    messages: 'chat_messages',
    model: 'selected_model',
    theme: 'theme',
};
```

`loadState()` restores those values on page load; `setTheme()` updates the root class, highlight.js theme, and `localStorage` value.

## Model picker

The model picker fetches `GET /api/chat/models`:

```javascript
const res = await fetch('/api/chat/models');
```

The API returns a JSON **list** of `{id, name, description}`. The UI collapses that list to the id → label dictionary shape used by the Blazor `ChatService`:

```javascript
const list = await res.json();
if (Array.isArray(list) && list.length > 0) {
    models = Object.fromEntries(
        list.filter((m) => m.id).map((m) => [m.id, m.name || m.id])
    );
}
```

If the API cannot be reached, the page falls back to a static catalogue of six models (`claude-haiku-4.5`, `gpt-4.1`, `gpt-5`, `claude-sonnet-4.5`, `claude-opus-4.5`, `gemini-2.5-pro`). A stale model saved in localStorage is replaced with `claude-haiku-4.5` when available, or the first live model otherwise.

### Internal-only models are filtered out

Accounts with internal entitlements can see models named like `GPT-5.6 Sol Fast (Internal only)`. Those names leak the account's access scope in a demo, stream, or screenshot, so `get_models` drops them before the list ever reaches the browser:

```python
visible = [(model_id, name) for model_id, name in live or [] if not _is_internal_only(name)]
```

The filter lives in the API rather than the UI, so both front-ends — this page and the Blazor `ChatService` — inherit it from one place, and the stale-selection fallback above quietly corrects a previously chosen internal model.

Note the trade-off the code comments call out: the SDK's `ModelInfo` exposes only `id`, `name`, `capabilities`, `policy`, and `billing`. There is no visibility or internal flag, so matching the display name is the only option available. `ChatController.IsInternalOnly` mirrors it in the .NET track.

## Streaming render

`sendMessage()` appends the user's message, adds an empty assistant placeholder, and posts to the SSE endpoint:

```javascript
const res = await fetch('/api/chat/stream', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prompt, model: selectedModel }),
});
```

⚠️ **That field name is load-bearing.** An earlier revision sent
`{ message: prompt, ... }`. `ChatRequest` declares `prompt`, and Pydantic
ignores unknown keys rather than rejecting them — so the request returned 200,
streamed a real response, and the typed prompt was silently discarded. Nothing
errored; the model simply answered an empty question.

That is the whole hazard of a permissive parser at a client/server boundary, and
it is why `tests/test_chat_contract.py` asserts the field `app.js` posts is the
field `ChatRequest` reads.

The response body is read with `fetch()`, `res.body.getReader()`, and `TextDecoder`:

```javascript
const reader = res.body.getReader();
const decoder = new TextDecoder();
let buffer = '';
```

Each read is decoded, split into lines, and the trailing incomplete line is kept for the next read:

```javascript
buffer += decoder.decode(value, { stream: true });
const lines = buffer.split('\n');
buffer = lines.pop() ?? '';
```

The parser looks for `data: ` frames, recognises `[DONE]` as the completion sentinel, and appends only `content` values. The server closes the response after `[DONE]`, so the reader loop exits.

```javascript
if (!line.startsWith('data: ')) continue;
const payload = line.slice(6);
if (payload === '[DONE]') continue;
```

## Render throttling

The UI repaints on a 50 ms interval, roughly 20 frames per second:

```javascript
const timer = setInterval(() => {
    if (!needsRender) return;
    needsRender = false;
    messages[messages.length - 1].content = content;
    renderMessages();
}, 50);
```

This is the direct analogue of the Blazor client's render timer. A fast model can produce hundreds of deltas; repainting for each token would waste browser work and make scrolling less stable.

## Input, suggestions, and theme

The input sends on button click or Enter without Shift, disables while streaming, and restores focus after the response completes. Suggestion chips use the same send path as typed input, with strings such as `Who are our highest spending customers?` and `Predict which segment customer C002 belongs to`. The theme button toggles dark/light mode and switches the active highlight.js stylesheet.

A completed exchange, rendered through the path described above:

![The chat UI showing a completed exchange: the user asked "Name three retail KPIs. One line each." and the assistant replied with a numbered Markdown list of Conversion Rate, Average Order Value (AOV), and Customer Retention Rate.](../screenshots/python-chat-ui-response.png)

## Related

- [Embedding the Copilot SDK](./01-copilot-sdk-integration.md)
- [Streaming responses over SSE](./02-sse-streaming.md)
- [The retail domain](./03-retail-analytics.md)
- Source: [`index.html`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/index.html), [`app.js`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/app.js), [`app.css`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/app.css), [`main.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/main.py), [`chat.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/routers/chat.py)
