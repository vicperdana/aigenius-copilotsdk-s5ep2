"""Diagnostic — NOT a lab. Reference code for ``on_permission_request``.

⚠️ Observed behaviour in this environment: the handler fires for **custom
tools** but NOT for **shell commands**. Running this sample, the model executed
``echo`` and reported its output while no ``[permission]`` line was printed —
the host Copilot CLI had already granted shell approval. The .NET version
reported the same gap.

The custom-tool half does work, and it is not optional: ``tools_sample`` must
pass a permission handler or its tool call is denied outright. That is a real
difference from .NET, where the equivalent sample needs no handler.

Treat this as a starting point for testing whether the hook engages in YOUR
environment — not as a working security control. See
docs/labs-python/03-tools/ for the full caveat, and
docs/labs/extra-governance-hooks/ for the shell-hook alternative.

One genuine Python advantage: the .NET project must suppress the ``GHCP001``
experimental-API build error to use permission decisions at all. Python exposes
them from ``copilot.rpc`` with no equivalent opt-in.
"""

from typing import Annotated

from copilot import (
    CopilotClient,
    PermissionRequest,
    PermissionRequestResult,
    SessionEvent,
    SessionEventType,
    ToolInvocation,
    define_tool,
)
from copilot.rpc import PermissionDecisionApproveOnce, PermissionDecisionReject

# Not re-exported at the package root in SDK 1.0.13 — import it from the module.
from copilot.session import PermissionInvocation
from pydantic import BaseModel, Field

from sdk_labs import model_picker
from sdk_labs._common import IdleWaiter, flatten


class CustomerParams(BaseModel):
    customer_id: Annotated[str, Field(description="Customer identifier, for example C003")]


@define_tool(description="Deletes a customer record permanently.")
def delete_customer(params: CustomerParams, _invocation: ToolInvocation) -> str:
    # Deliberately inert — this sample must never mutate anything.
    print(f"  [tool] delete_customer({params.customer_id}) — no-op in this sample")
    return f"Pretended to delete {params.customer_id}."


@define_tool(description="Gets the number of transactions for a customer.")
def count_transactions(params: CustomerParams, _invocation: ToolInvocation) -> str:
    print(f"  [tool] count_transactions({params.customer_id}) -> 2")
    return f"{params.customer_id} has 2 transactions."


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


async def run(requested_model_id: str | None) -> int:
    print("== Diagnostic: permissions (not a lab) ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        session = await client.create_session(
            model=model_id,
            streaming=False,
            tools=[count_transactions, delete_customer],
            on_permission_request=on_permission_request,
        )

        async with session:
            waiter = IdleWaiter()

            def on_event(evt: SessionEvent) -> None:
                if evt.type is SessionEventType.PERMISSION_REQUESTED:
                    print(f"  [permission] event: request_id={evt.data.request_id}")
                elif evt.type is SessionEventType.PERMISSION_COMPLETED:
                    outcome = getattr(evt.data.result, "kind", evt.data.result)
                    print(f"  [permission] result: {outcome}")
                elif evt.type is SessionEventType.ASSISTANT_MESSAGE and evt.data.content:
                    print(f"\nAssistant: {flatten(evt.data.content)}")
                elif evt.type is SessionEventType.SESSION_ERROR:
                    print(f"ERROR: {evt.data.message}")
                waiter.handle(evt)

            session.on(on_event)

            print("Prompt: asking the model to run a shell command\n")
            await session.send(
                "Run the shell command `echo hello-from-lab-05` and tell me its output."
            )

            await waiter.wait()

    return 0
