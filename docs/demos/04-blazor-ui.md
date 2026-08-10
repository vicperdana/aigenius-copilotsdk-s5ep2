# The Blazor front end

This walkthrough explains the Blazor WebAssembly client that sits in front of
the retail analytics API. You will see how it connects to the API, streams chat
responses into the message list, persists local settings, and keeps the model
picker aligned with the live model list.

## App shape and ports

The front end is a Blazor WebAssembly app under
[`AgentHQDemo.Web`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator/AgentHQDemo.Web).

Throughout this repository the two services are started with an explicit
`--urls`, which overrides the launch profile:

```bash
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Web --urls "http://localhost:5051"
```

That is why the docs, diagrams, and labs all refer to **5051** for the UI and
**5050** for the API. ⚠️ The checked-in launch profiles default to *different*
ports — 5240 for the Web project and 5167 for the API — so running without
`--urls` (or pressing F5 in an IDE) will serve on those instead, and the UI's
default API base address of `http://localhost:5050` will no longer match.
Either pass `--urls` as documented, or set `ApiBaseUrl` to match.

[`AgentHQDemo.Web/Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Program.cs)
configures the API base address:

```csharp
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5050";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
```

The API enables CORS in
[`AgentHQDemo.Api/Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Program.cs)
with a default policy that allows any origin, method, and header. That lets the
WebAssembly app served from its local development URL call the configured API
base address during the demo.

## `Home.razor`

[`Home.razor`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Pages/Home.razor) is
the main chat page. It owns the page state:

- `Messages`: the ordered chat transcript.
- `SelectedModel`: the currently selected Copilot model.
- `Models`: a dictionary of model ID to display name.
- `IsDarkTheme`: the current theme flag.
- `IsStreaming`: whether an assistant response is in progress.

When there are no messages, the page shows a welcome panel and
`SuggestionChips`. Once messages exist, it renders each one with the `Message`
component.

## Streaming state and scrolling

`Home.razor.SendMessage` adds the user's message, stores it, then appends an
empty assistant placeholder. While `IsStreaming` is true, the input is disabled
and the assistant message is updated as chunks arrive from
`ChatService.StreamChatAsync`.

The page batches UI updates with a `Timer` at roughly 20 frames per second
instead of rendering for every token. After renders it calls JavaScript helpers
to scroll the message container to the bottom and highlight code blocks.

## Local storage persistence

[`StorageService`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Services/StorageService.cs)
wraps `Blazored.LocalStorage`. It stores three local values:

| Key | Used for |
| --- | --- |
| `chat_messages` | The persisted `ChatMessage` transcript. |
| `selected_model` | The model restored on the next page load. |
| `theme` | The saved `dark` or `light` theme. |

`Home.razor.OnInitializedAsync` loads all three values before fetching the model
list.

## Model picker

[`Header.razor`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Components/Header.razor)
renders the model picker. It receives the live `Models` dictionary and binds the
selected option to `SelectedModel`:

```razor
<select id="model-select" @bind="SelectedModel" @bind:after="OnModelChanged">
```

When the user changes the selection, `Header.OnModelChanged` invokes the
`SelectedModelChanged` callback. `Home.razor.OnModelChanged` updates local state
and saves the new model through `StorageService.SetSelectedModelAsync`.

## Fetching live models

[`ChatService.GetModelsAsync`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Services/ChatService.cs)
loads model metadata from the API:

```csharp
var models = await _http.GetFromJsonAsync<List<ApiModel>>("/api/chat/models");
```

If the API returns at least one model, the service converts it into the
`Dictionary<string, string>` consumed by `Header.razor`. If the API is
unreachable or returns unusable data, it falls back to `ChatService.AvailableModels`,
the static catalogue used for offline resilience.

## Guarding against stale saved models

A model saved in local storage may no longer be available to the signed-in
account. `Home.razor.OnInitializedAsync` handles that after fetching live models:

```csharp
if (!Models.ContainsKey(SelectedModel))
{
    SelectedModel = Models.ContainsKey("claude-haiku-4.5")
        ? "claude-haiku-4.5"
        : Models.Keys.First();
}
```

The page then persists the replacement model. This protects users from an old
localStorage value that would otherwise cause chat requests to use a model the
API no longer offers.

## Supporting components

[`ChatInput`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Components/ChatInput.razor)
provides the textarea and send button. It sends on button click or Enter without
Shift, disables input while loading, trims blank messages, and focuses the input
after first render.

[`Message`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Components/Message.razor)
renders user and assistant messages. Empty assistant content displays a typing
indicator; non-empty content is rendered from markdown using Markdig, with code
blocks marked for JavaScript highlighting behaviour.

[`SuggestionChips`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Components/SuggestionChips.razor)
shows predefined retail analytics prompts. Selecting a chip sends that prompt
through the same `Home.razor.SendMessage` path as typed input.

## Related

- [Embedding the Copilot SDK](./01-copilot-sdk-integration.md)
- [Streaming responses over SSE](./02-sse-streaming.md)
- [The retail domain](./03-retail-analytics.md)
- Source:
  [`Home.razor`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Pages/Home.razor),
  [`Header.razor`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Components/Header.razor),
  [`ChatService.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Services/ChatService.cs),
  [`StorageService.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Services/StorageService.cs),
  [`AgentHQDemo.Web/Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Web/Program.cs),
  [`AgentHQDemo.Api/Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Program.cs),
  [`Components`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator/AgentHQDemo.Web/Components)
