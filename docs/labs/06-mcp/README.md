# Lab 06 — Model Context Protocol (MCP)

**Goal:** attach a Model Context Protocol server to a Copilot SDK session so
an agent gains tools you did not write.

**Time:** ~20 minutes

**Prerequisites:** [Lab 05](../05-sessions/) complete, plus internet access for
the HTTP MCP server.

## Step 1 — Understand what MCP adds

In [Lab 03](../03-tools/) you gave the model tools by writing C# functions and
registering them yourself. That is powerful, but every capability is still code
you own, test and maintain.

MCP changes the shape of the problem. A Model Context Protocol server exposes a
whole toolset over a standard protocol, and the SDK can attach that server to a
session. The model then discovers and calls tools from that server as part of
its normal reasoning loop.

For this lab the external toolset is Microsoft Learn. Instead of writing a
`SearchDocsAsync` function, you connect to the Learn MCP server and let the
agent consult product documentation directly.

## Step 2 — Choose the MCP transport

The SDK has two MCP server config types:

```csharp
McpServers = new Dictionary<string, McpServerConfig>
{
    ["microsoft.docs.mcp"] = new McpHttpServerConfig
    {
        Url = "https://learn.microsoft.com/api/mcp"
    }
};
```

Use `McpHttpServerConfig` when the server is already running somewhere and can
be reached over HTTP. That is the case here: Microsoft Learn hosts the server at
`https://learn.microsoft.com/api/mcp`, so there is nothing to install locally.

Use `McpStdioServerConfig` when the SDK should spawn a local MCP process and
communicate with it over standard input and output. That shape is common for
local filesystem tools, database helpers or language-specific MCP servers that
run as command-line programs on your machine.

Both transports produce the same result from the model's point of view: named
MCP tools become available inside the session.

## Step 3 — Configure and run the sample

Open
[`McpSample.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/samples/SdkLabs/McpSample.cs)
and find the `SessionConfig`:

```csharp
var modelId = await ModelPicker.PickAsync(client, requestedModelId);

var config = new SessionConfig
{
    Model = modelId,
    Streaming = false,
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["microsoft.docs.mcp"] = new McpHttpServerConfig
        {
            Url = "https://learn.microsoft.com/api/mcp"
        }
    },
    OnPermissionRequest = PermissionHandler.ApproveAll
};
```

`SessionConfig.McpServers` is an `IDictionary<string, McpServerConfig>`. The
keys are the server names, and the values are the transport-specific server
configuration objects.

The sample also sets `OnPermissionRequest = PermissionHandler.ApproveAll`. In
our runs the host CLI appeared to pre-approve MCP tool use, so that handler was
not observed firing. Treat it as belt-and-braces rather than proof that this
line is always required. The permission caveat is similar to the one covered in
[Lab 03](../03-tools/).

Run the sample:

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- mcp
```

Expected output from a verified run:

```
== Lab 06: mcp ==

Model: claude-haiku-4.5
Prompt: asking the model to consult Microsoft Learn docs

  [mcp] SessionMcpServersLoadedEvent

Assistant: I don't have dedicated "Microsoft Learn tools" in my available toolset.
However, I can use the `web_fetch` tool to retrieve current information.
  [tool] ToolExecutionStartEvent: web_fetch
  [tool] ToolExecutionCompleteEvent: web_fetch (success=True)

Assistant: Based on Microsoft Learn documentation: **Azure Container Apps is a
serverless platform for running containerized applications without managing the
underlying infrastructure.** It supports API endpoints, background jobs,
event-driven processing and microservices, scaling automatically.

⚠️  No MCP tool was invoked.
    The model used non-MCP tool(s) instead: web_fetch
    The answer may have come from the model's own knowledge or a built-in
    tool rather than Microsoft Learn. Check the server is reachable and that
    its tools were loaded — look for the [mcp] lines above.
```

⚠️ **Read that output carefully — this is the whole point of the lab.**

The answer looks authoritative and even cites Microsoft Learn. It is also
**not** an MCP result. `SessionMcpServersLoadedEvent` fired, so the server
config was accepted, but its tools were never offered to the model — so the
model fell back to the built-in `web_fetch` and produced a plausible answer
anyway.

Without the check at the end you would have called this a successful MCP demo.
That is exactly the false pass this sample exists to prevent, and it is why the
sample exits **non-zero** here.

> **Status in this environment:** the Learn MCP server loads but does not
> surface tools to the session. Treat the ⚠️ path above as the expected output
> until that is resolved. If MCP tools *do* load for you, the final line reads
> `✅ MCP tool(s) invoked: <server>/<tool>` and the exit code is 0.

## Step 4 — Check that the MCP tools were used

The sample subscribes to SDK events and logs the ones that matter:

- `SessionMcpServersLoadedEvent` — the server configuration was accepted
- `McpToolsListChangedEvent` — the server published its tool list
- `ToolExecutionStartEvent` / `ToolExecutionCompleteEvent` — a tool actually ran

The critical detail is how a tool is judged to be *MCP*. `ToolExecutionStartEvent`
carries an `McpServerName`; only executions where that is set count. A built-in
such as `web_fetch` has no server name, so it is logged but never counted as
success:

```csharp
if (!string.IsNullOrWhiteSpace(start.Data.McpServerName))
{
    mcpTools.Add(toolName);
}
```

⚠️ An earlier version of this sample counted *any* tool execution and happily
reported `✅ MCP tool(s) invoked: web_fetch` — success for a tool that has
nothing to do with MCP. Counting the wrong thing is worse than not checking,
because it manufactures confidence.

If no MCP tool ran, the sample prints:

```text
⚠️  No MCP tool was invoked.
    The model used non-MCP tool(s) instead: web_fetch
```

and exits non-zero so scripts cannot mistake a plausible model-only answer for
a successful MCP-backed run. This matters because Azure Container Apps is public
knowledge, so a model can answer from its own training data even when no MCP
tool was available.

## Step 5 — Compare with the editor MCP configuration

This repository already has the same server configured for VS Code in
[`.vscode/mcp.json`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/.vscode/mcp.json):

```json
{
  "servers": {
    "microsoft.docs.mcp": {
      "type": "http",
      "url": "https://learn.microsoft.com/api/mcp"
    }
  }
}
```

That file is for the editor. `SessionConfig.McpServers` is for the Copilot SDK
session running inside your app or sample. They are different consumers, but
they speak to the same server over the same protocol.

That is the point of MCP: one protocol, many clients. The same tool server can
serve an editor, a CLI, a test harness or an application agent.

## ⚠️ Traps

- `McpServers` is not a `Dictionary<string, object>`. It is an
  `IDictionary<string, McpServerConfig>`, so this common shortcut fails with a
  compile error such as `CS0266` because the value type cannot be converted.
- The Learn server is reached over the network. If it is unreachable, the model
  has no MCP tools and may quietly answer from its own knowledge. The sample now
  exits non-zero unless it observes a `ToolExecutionStartEvent`.
- MCP servers are third-party code and can expose powerful capabilities. Vet the
  server, its permissions and its data access before adding it to an agent that
  handles real work.

## 💡 Extra credit

Try one of these after the basic run succeeds:

- Add a second MCP server and compare how the model chooses between toolsets.
- Add the server name to `DisabledMcpServers`, rerun the same prompt and compare
  the answer with and without Microsoft Learn tools available.
- Explore related SDK configuration such as `McpOAuthTokenStorage`,
  `GitHubMcpToolConfig` and `EnableMcpApps` when you need authenticated or
  richer MCP scenarios.

## ✅ Checkpoint

You can now explain:

- [x] How MCP differs from tools you write directly in C#
- [x] When to use `McpHttpServerConfig` versus `McpStdioServerConfig`
- [x] How `SessionConfig.McpServers` attaches a server by name
- [x] Why a plausible answer is not proof that MCP tools were called
- [x] How `.vscode/mcp.json` and SDK configuration can target the same server

## Related

- Previous: [Lab 05 — Sessions](../05-sessions/)
- Next: [Lab 07 — Wrap-up](../07-wrap-up/)
- [Demo: Copilot SDK integration](../../demos/01-copilot-sdk-integration.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
