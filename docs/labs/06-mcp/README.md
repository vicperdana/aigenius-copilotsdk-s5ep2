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
var config = new SessionConfig
{
    Model = "claude-haiku-4.5",
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
== Lab 07: mcp ==

Prompt: asking the model to consult Microsoft Learn docs


Assistant: Based on Microsoft Learn documentation:  **Azure Container Apps** is a serverless platform that enables you to run containerized applications without managing underlying infrastructure, automatically handling scaling and deployment. It's ideal for deploying API endpoints, background jobs, event-driven processing, and microservices while reducing operational overhead and costs.
```

The banner still says `Lab 07` because the sample was written before the labs
were renumbered. The assistant's exact wording can vary between runs, but it
should answer as though it consulted Microsoft Learn.

## Step 4 — Check that the MCP tools were used

The visible clue is the answer itself: it says it is based on Microsoft Learn
documentation and gives a docs-style definition of Azure Container Apps.

For a stronger signal, reuse the idea from [Lab 04](../04-events/): subscribe
to SDK events and log tool-related events while the session runs. That lets you
observe the model deciding to call an MCP tool rather than only judging the
final answer.

This matters because a plausible answer is not enough proof. Azure Container
Apps is public knowledge, so a model can answer from its own training data even
when no MCP tool was available.

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
  has no MCP tools and may quietly answer from its own knowledge. That is easy
  to mistake for success unless you log tool events.
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
