# Streaming responses over SSE

This walkthrough follows a chat response from the ASP.NET Core API to the
Blazor WebAssembly browser client. You will learn the exact SSE wire format, why
flushes matter, and how the client parses streamed chunks.

## API entry point

[`ChatController.StreamChat`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/ChatController.cs)
handles `POST /api/chat/stream`. It accepts a `ChatRequest`, chooses the
requested model or the `claude-haiku-4.5` default, and configures the response as
Server-Sent Events:

```csharp
Response.ContentType = "text/event-stream";
Response.Headers.CacheControl = "no-cache";
Response.Headers.Connection = "keep-alive";
```

Those headers tell intermediaries and the browser that this is a long-lived
stream, not a normal JSON response that should be buffered until completion.

## Wire format

For each chunk from
[`CopilotChatService.ChatStreamAsync`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Services/CopilotChatService.cs),
the controller serialises a small JSON object and writes one SSE message:

```text
data: {"content":"..."}

```

When the stream finishes normally, the endpoint writes a sentinel:

```text
data: [DONE]

```

If an exception is raised after the stream has started, the controller writes an
error event in the same SSE data channel:

```text
data: {"error":"..."}

```

At that point it cannot reliably switch to an HTTP error status. The status code
and response headers have already been sent, so the only useful way to report a
late failure is inside the stream payload.

## Flushing each chunk

After every content chunk, `ChatController.StreamChat` flushes the body:

```csharp
await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
await Response.Body.FlushAsync(cancellationToken);
```

Without `FlushAsync`, the server, host, proxy, or browser can buffer data. The
SDK may be producing deltas correctly, but the user would see nothing until a
buffer fills or the request ends, which makes streaming appear broken.

## Cancellation

`StreamChat` accepts the request `CancellationToken` supplied by ASP.NET Core.
The controller passes it into `CopilotChatService.ChatStreamAsync`, checks
`IsCancellationRequested` during the loop, and also passes it to `WriteAsync` and
`FlushAsync`. If the browser tab is closed or the request is abandoned, the API
has a path to stop writing and unwind the streaming work.

## Browser client

The Blazor client code lives in
[`ChatService.StreamChatAsync`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Services/ChatService.cs).
It posts to `/api/chat/stream` with `HttpCompletionOption.ResponseHeadersRead`:

```csharp
using var response = await _http.SendAsync(
    httpRequest,
    HttpCompletionOption.ResponseHeadersRead);
```

`ResponseHeadersRead` is important because it returns as soon as the response
headers arrive. The client can then read the body stream line by line instead of
waiting for the whole response.

The parser ignores blank lines, looks for `data: ` prefixes, stops on `[DONE]`,
and parses JSON data events. `content` values are yielded to the UI; `error`
values become exceptions.

## Retail analytics system prompt

`ChatService.StreamChatAsync` sends a default system message when the caller does
not provide one. That prompt frames the assistant as a retail analytics assistant
for a grocery retailer, gives it the demo context, and asks for data-driven
business insights using markdown tables and bullet points. It also tells the
assistant not to modify code or suggest code changes.

[`Home.razor`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Pages/Home.razor)
passes the user's prompt and selected model to `ChatService.StreamChatAsync`.
The default system prompt is therefore applied by the client service before the
request reaches the API.

## Try it with curl

Point the request at the port your local API uses. For example, if the API is
listening on the Web client's default API base address, send a streaming request
with `curl -N` so curl does not buffer the response:

```bash
curl -N -X POST http://localhost:5050/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Summarise customer C003 and recommend a segment.",
    "model": "claude-haiku-4.5",
    "systemMessage": "You are a retail analytics assistant."
  }'
```

You should see multiple `data: {"content":"..."}` messages followed by
`data: [DONE]`.

## Related

- [Embedding the Copilot SDK](./01-copilot-sdk-integration.md)
- [The retail domain](./03-retail-analytics.md)
- [The Blazor front end](./04-blazor-ui.md)
- Source:
  [`ChatController.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/ChatController.cs),
  [`CopilotChatService.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Services/CopilotChatService.cs),
  [`ChatService.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Services/ChatService.cs),
  [`Home.razor`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Pages/Home.razor)
