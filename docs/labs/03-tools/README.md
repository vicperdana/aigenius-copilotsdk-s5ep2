# Lab 03 — Tools

**Goal:** replace static prompt context with a real C# function the model can
call on demand using `CopilotTool.DefineTool` and `SessionConfig.Tools`.

**Time:** ~20 minutes

**Prerequisites:** [Lab 02](../02-first-chat/) complete.

## Step 1 — Why tools

In Lab 02, you grounded the assistant by putting retail facts into a system
message. That works for tiny examples, but it has three problems:

1. The context is static — it only knows what you pasted in up front
2. The model has to guess which facts matter
3. Every fact burns tokens, even when the answer does not need it

A tool changes the shape of the problem. Instead of hoping the prompt contains
the right data, you register a C# function. The model decides when it needs
that function, asks the SDK to call it, receives the result, then writes the
final answer.

## Step 2 — Inspect the tool shape

Open
[`ToolsSample.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/samples/SdkLabs/ToolsSample.cs)
and find `GetCustomerTotal`.

A Copilot SDK tool starts as an ordinary C# method:

```csharp
[Description("Gets the total amount a given retail customer has spent.")]
private static string GetCustomerTotal(
    [Description("Customer identifier, for example C003")] string customerId)
{
    ...
}
```

The important part is the `[Description]` metadata from
`System.ComponentModel`.

Those descriptions are the model's API documentation. If the method description
is vague, the model may miss the tool. If a parameter description is vague, it
may pass the wrong value. Write descriptions the same way you would document a
public API for another developer.

## Step 3 — Register the tool

`CopilotTool.DefineTool(GetCustomerTotal)` turns the method into an
`AIFunction`. You then assign that function to the session configuration:

```csharp
var totalTool = CopilotTool.DefineTool(GetCustomerTotal);

var config = new SessionConfig
{
    Model = "claude-haiku-4.5",
    Streaming = false,
    Tools = [totalTool]
};
```

That `Tools = [totalTool]` line is the difference between "the model has some
textual context" and "the model can ask the host application to do real work".

## Step 4 — Run it

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- tools
```

Expected output:

```
== Lab 03: tools ==

Prompt: How much has customer C003 spent in total?


Assistant: 
  [tool] GetCustomerTotal(C003) -> $1,700.00

Assistant: Customer C003 has spent a total of **$1,700.00** across 2 transactions.
```

⚠️ Notice the empty first `Assistant:` line. That is real: the assistant
message event fires around the tool call, before the final natural-language
answer is composed.

## Step 5 — Trace what happened

The run has four moving parts:

1. The prompt asks for the total spend for customer `C003`
2. The model decides that the registered tool is the right way to answer
3. The SDK invokes the C# method, producing the `[tool]` line
4. The tool result is fed back to the model, which writes the final answer

The `[tool] GetCustomerTotal(C003) -> $1,700.00` line is not simulated output.
It is printed by the real `GetCustomerTotal` method while the SDK is handling
the model's tool call.

## Step 6 — Experiment

Try a customer that does not exist, for example `C999`. The tool handles that
case by returning a normal string instead of throwing:

```csharp
Prompt = "How much has customer C999 spent in total? Use the available tool."
```

Re-run the sample and check that the assistant reports that no transactions
were found.

Then try a prompt that does not need retail data:

```csharp
Prompt = "In one short sentence, define average order value."
```

The model should answer directly. Because no customer lookup is needed, the
`[tool]` line should not appear.

## Step 7 — Control tool execution with permissions

The SDK also exposes an in-process permission hook:

```csharp
OnPermissionRequest = (request, invocation) =>
{
    return Task.FromResult(PermissionDecision.ApproveOnce());
}
```

The signature is:

```csharp
Func<PermissionRequest, PermissionInvocation, Task<PermissionDecision>>
```

Decisions come from `GitHub.Copilot.Rpc.PermissionDecision`, including
`ApproveOnce()` and `Reject(string feedback)`. There is also a built-in
shortcut, `PermissionHandler.ApproveAll`, for samples where every request is
allowed.

⚠️ In GitHub Copilot SDK v1.0.9 this permission-decision API is marked
experimental. Using `GitHub.Copilot.Rpc.PermissionDecision` raises build error
`GHCP001` unless it is suppressed. The samples project does that deliberately
in
[`SdkLabs.csproj`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/samples/SdkLabs/SdkLabs.csproj):

```xml
<NoWarn>$(NoWarn);GHCP001</NoWarn>
```

⚠️ Be scrupulously careful about relying on this hook as an enforcement point.
When we tested it, the handler was **never invoked** — even a handler that
rejected every request still let the command run, because the host CLI had
pre-granted tool approval. Treat `OnPermissionRequest` as a hook that only
engages where the host defers to it. Verify that it fires in **your**
environment before relying on it as a control.

See
[`PermissionsSample.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/samples/SdkLabs/PermissionsSample.cs)
for reference code. For a shell-hook governance alternative, see
[extra-governance-hooks](../extra-governance-hooks/).

`CopilotToolOptions.SkipPermission` also exists for opting a tool out of the
permission flow when that is appropriate for your host.

## ✅ Checkpoint

You can now explain:

- [x] Why tools are better than stuffing dynamic data into a system message
- [x] How `[Description]` attributes guide tool selection and arguments
- [x] How `CopilotTool.DefineTool` registers a C# method as an `AIFunction`
- [x] How `SessionConfig.Tools` makes that function available to the model
- [x] Why permission hooks must be verified in the host you actually run

## 💡 Extra credit

Add a second tool that returns the product categories a customer has purchased
from, such as `Electronics` and `Fashion` for `C003`. Give the method and its
parameter precise `[Description]` attributes, register it beside
`GetCustomerTotal`, then ask:

```text
Which categories has customer C003 bought from, and how much have they spent?
```

Check whether the model calls one tool, both tools, or answers directly.

## Related

- Previous: [Lab 02 — Your first streaming chat](../02-first-chat/)
- Next: [Lab 04 — Events](../04-events/)
- [Demo: Copilot SDK integration](../../demos/01-copilot-sdk-integration.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
