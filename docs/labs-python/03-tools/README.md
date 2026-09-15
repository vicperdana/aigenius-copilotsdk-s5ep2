# Lab 03 — Tools

**Goal:** replace static prompt context with a real Python function the model can
call on demand using `@define_tool`, Pydantic parameter metadata, and
`client.create_session(..., tools=[...])`.

**Time:** ~20 minutes

**Prerequisites:** [Lab 02](../02-first-chat/) complete.

## Step 1 — Why tools

In Lab 02, you grounded the assistant by sending retail facts in a system
message. That works for tiny examples, but it has three problems:

1. The context is static — it only knows what you pasted in up front
2. The model has to guess which facts matter
3. Every fact burns tokens, even when the answer does not need it

A tool changes the shape of the problem. Instead of hoping the prompt contains
the right data, you register a Python function. The model decides when it needs
that function, asks the SDK to call it, receives the result, then writes the
final answer.

## Step 2 — Inspect the tool shape

Open
[`sdk_labs/tools_sample.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/tools_sample.py)
and find `get_customer_total`.

A Copilot SDK tool starts as an ordinary Python function with a Pydantic params
model:

```python
class GetCustomerTotalParams(BaseModel):
    """Parameter schema sent to the model.

    Where C# reads ``[Description]`` attributes off the method signature, Python
    describes parameters with a Pydantic model — the field descriptions are what
    the model sees.
    """

    customer_id: Annotated[str, Field(description="Customer identifier, for example C003")]


@define_tool(description="Gets the total amount a given retail customer has spent.")
def get_customer_total(params: GetCustomerTotalParams, _invocation: ToolInvocation) -> str:
    matches = [t for t in TRANSACTIONS if t[0].casefold() == params.customer_id.casefold()]

    if not matches:
        return f"No transactions found for {params.customer_id}."

    total = sum(t[1] for t in matches)
    print(f"  [tool] get_customer_total({params.customer_id}) -> ${total:,.2f}")
    return f"{params.customer_id} has {len(matches)} transactions totalling ${total:,.2f}."
```

The required signature is
`(params: SomePydanticModel, _invocation: ToolInvocation) -> str`.

The key teaching difference from C# is metadata. .NET uses
`[Description]` attributes on the method and parameters. Python uses:

- `@define_tool(description=...)` for the tool description
- `Field(description=...)` on Pydantic model fields for parameter descriptions

Those descriptions are the model's API documentation. Vague descriptions lead to
missed tools or wrong arguments, so write them like a public API.

## Step 3 — Register the tool

The sample creates a client, picks a model, then registers the tool when it
creates the session:

```python
async with CopilotClient() as client:
    model_id = await model_picker.pick(client, requested_model_id)
    if model_id is None:
        return 1

    session = await client.create_session(
        model=model_id,
        streaming=False,
        tools=[get_customer_total],
        # Required in Python, unlike .NET: the runtime asks permission before
        # invoking a custom tool, and with no handler the call is denied and
        # the model reports a permission error instead of an answer.
        on_permission_request=PermissionHandler.approve_all,
    )
```

That `tools=[get_customer_total]` line is the difference between "the model has
some textual context" and "the model can ask the host application to do real
work".

`model_picker` keeps `claude-haiku-4.5` as the preferred model for these labs,
but falls back to a concrete model available to your account. Override it with:

```bash
uv run python -m sdk_labs tools --model gpt-5
```

## Step 4 — Do not skip the permission handler

⚠️ This is genuinely different from the .NET sample.

In Python, custom tool calls are denied unless you pass `on_permission_request`
when creating the session. Without it, the model receives a permission failure
instead of the tool result and answers with an error.

For a lab sample where every request is allowed, use:

```python
on_permission_request=PermissionHandler.approve_all
```

For production code, provide a policy function instead. Step 8 covers the
security caveat.

## Step 5 — Run it

From `src/AgentOrchestrator-python`:

```bash
uv run python -m sdk_labs tools
```

Expected output:

```text
== Lab 03: tools ==

Model: claude-haiku-4.5
Prompt: How much has customer C003 spent in total?

  [tool] get_customer_total(C003) -> $1,700.00

Assistant: Customer C003 has spent a total of **$1,700.00** across 2 transactions.
```

⚠️ Notice what is **not** there: unlike the .NET transcript, the Python sample
has no empty first `Assistant:` line. The first assistant event only carries the
tool request, so `tools_sample.py` deliberately skips assistant messages whose
content is empty.

## Step 6 — Trace what happened

The run has four moving parts:

1. The prompt asks for the total spend for customer `C003`
2. The model decides that the registered tool is the right way to answer
3. The SDK invokes the Python function, producing the `[tool]` line
4. The tool result is fed back to the model, which writes the final answer

The `[tool] get_customer_total(C003) -> $1,700.00` line is not simulated output.
It is printed by the real `get_customer_total` function while the SDK is
handling the model's tool call.

The tool returns a normal string, not a custom SDK result object:

```python
return f"{params.customer_id} has {len(matches)} transactions totalling ${total:,.2f}."
```

## Step 7 — Understand event delivery

The sample still uses the event model you saw in Lab 02. Python events are
push-only callbacks: `session.on(handler)` registers the handler and returns an
unsubscribe callable. There is no async iterator.

`SessionEvent` is one dataclass with fields such as `data`, `id`, `timestamp`,
and `type`. The `type` is a `SessionEventType` enum, so Python branches on
`evt.type`. This differs fundamentally from the .NET one-subclass-per-event
pattern.

The shared `IdleWaiter` in
[`sdk_labs/_common.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/_common.py)
keeps samples from waiting forever if the session never reaches idle.

## Step 8 — Control tool execution with permissions

The SDK exposes an in-process permission hook through `on_permission_request`.
A custom policy can inspect the request and return one of the decision objects
from `copilot.rpc`, for example:

```python
from copilot.rpc import PermissionDecisionApproveOnce, PermissionDecisionReject

# Not re-exported at the package root in SDK 1.0.13 — import it from the module.
from copilot.session import PermissionInvocation
```

The reference diagnostic implements:

```python
def on_permission_request(
    request: PermissionRequest, invocation: PermissionInvocation
) -> PermissionRequestResult:
    # `kind` is a class attribute on each request type ("shell", "custom-tool",
    # "write", …), where the C# version reads a `Kind` property off one type.
    kind = getattr(request, "kind", "")
    tool_name = getattr(request, "tool_name", "") or ""
    print(f"  [permission] requested: kind={kind} tool={tool_name or '(n/a)'}")

    # Policy: allow reads, refuse anything destructive.
    if "delete" in f"{kind} {tool_name}".casefold():
        print("  [permission] -> REJECTED by policy")
        return PermissionDecisionReject(
            feedback="Destructive operations are not permitted in this demo."
        )

    print("  [permission] -> approved once")
    return PermissionDecisionApproveOnce()
```

Python advantage: there is **no `GHCP001` suppression** step. The .NET project
must suppress an experimental-API build error to use permission decisions.
Python exposes them directly from `copilot.rpc` with no opt-in.

⚠️ Permission handler caveat: the handler fires for **custom tools**, and the
Lab 03 tool needs it. It was **not** observed firing for shell commands. Running
the permissions diagnostic, the model executed `echo` and reported output with
no `[permission]` line printed, because the host Copilot CLI already grants
shell approval.

Treat `on_permission_request` as a custom-tool policy hook, not a general
enforcement point. Verify it fires in **your** environment before relying on it.

Reference code lives in
[`sdk_labs/permissions_sample.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/permissions_sample.py).
For a shell-hook governance alternative, see
[extra-governance-hooks](../../labs/extra-governance-hooks/).

## Step 9 — Experiment

Try a customer that does not exist, for example `C999`. Change the prompt in
`tools_sample.py` to:

```python
await session.send(
    "How much has customer C999 spent in total? Use the available tool."
)
```

Re-run the sample and check that the assistant reports no transactions were
found.

Then try a prompt that does not need retail data:

```python
await session.send("In one short sentence, define average order value.")
```

The model should answer directly. Because no customer lookup is needed, the
`[tool]` line should not appear.

## ✅ Checkpoint

You can now explain:

- [x] Why tools are better than stuffing dynamic data into a system message
- [x] How `@define_tool(description=...)` describes a Python tool
- [x] How Pydantic `Field(description=...)` describes tool parameters
- [x] How `tools=[get_customer_total]` makes the function available to the model
- [x] Why Python custom tools need `on_permission_request`
- [x] Why Python events branch on `evt.type`
- [x] Why permission hooks must be verified in the host you actually run

## 💡 Extra credit

Add a second tool that returns the product categories a customer has purchased
from, such as `Electronics` and `Fashion` for `C003`.

Use a second Pydantic params model or reuse `GetCustomerTotalParams`, give the
tool a precise description, register it beside `get_customer_total`, then ask:

```text
Which categories has customer C003 bought from, and how much have they spent?
```

Check whether the model calls one tool, both tools, or answers directly.

## Related

- Previous: [Lab 02 — Your first streaming chat](../02-first-chat/)
- Next: [Lab 04 — Events](../04-events/)
- [Demo: Copilot SDK integration](../../demos-python/01-copilot-sdk-integration.md)
- [Extra — Governance hooks](../../labs/extra-governance-hooks/)
