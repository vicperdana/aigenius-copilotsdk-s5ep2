"""AI Genius S5E2 — LIVE CODE-ALONG (starter).

Run one phase at a time::

    uv run python -m sdk_labs codealong --phase 1

Only the Phase 3 block marked TYPE THIS LIVE is edited on stage.
Fall back to ``code_along_final`` at any time.
"""

from typing import Annotated

from copilot import (
    CopilotClient,
    PermissionHandler,
    SessionEvent,
    SessionEventType,
    ToolInvocation,
    define_tool,
)
from pydantic import BaseModel, Field

from sdk_labs import model_picker
from sdk_labs._common import IdleWaiter, flatten, send_and_print

# Phase 0 — in-memory demo data
TRANSACTIONS: list[tuple[str, float, str]] = [
    ("C001", 245.50, "Grocery"),
    ("C001", 89.99, "Electronics"),
    ("C002", 32.00, "Grocery"),
    ("C002", 15.50, "Health"),
    ("C003", 1250.00, "Electronics"),
    ("C003", 450.00, "Fashion"),
    ("C004", 12.99, "Grocery"),
    ("C004", 8.50, "Grocery"),
    ("C005", 675.00, "Electronics"),
    ("C005", 320.00, "Fashion"),
]


# Phase 1 — Hello World: a client and a session
async def phase1_hello_world(requested_model_id: str | None) -> int:
    print("== Phase 1: hello world ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        # Streaming emits token deltas as they arrive.
        session = await client.create_session(model=model_id, streaming=True)

        async with session:
            waiter = IdleWaiter()

            def on_event(evt: SessionEvent) -> None:
                if evt.type is SessionEventType.ASSISTANT_MESSAGE_DELTA:
                    print(evt.data.delta_content or "", end="", flush=True)
                elif evt.type is SessionEventType.SESSION_ERROR:
                    print(f"\nERROR: {evt.data.message}")
                waiter.handle(evt)

            session.on(on_event)

            print("Prompt: Name three retail KPIs. One line each.\n")
            await session.send("Name three retail KPIs. One line each.")
            await waiter.wait()
            print()

    return 0


# Phase 2 — Events: what the session emits
async def phase2_events(requested_model_id: str | None) -> int:
    print("== Phase 2: events ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        session = await client.create_session(model=model_id, streaming=True)

        async with session:
            waiter = IdleWaiter()
            counters = {"order": 0, "deltas": 0}
            finished = False

            # Count streaming deltas so the lifecycle remains readable.
            def on_event(evt: SessionEvent) -> None:
                nonlocal finished
                name = evt.type.value

                # Keep shutdown events from printing after the summary.
                if finished:
                    return

                if "delta" in name.lower():
                    counters["deltas"] += 1
                    return

                counters["order"] += 1
                print(f"{counters['order']:3d}. {name}")

                if evt.type is SessionEventType.SESSION_IDLE:
                    finished = True

                waiter.handle(evt)

            session.on(on_event)

            await session.send("In one sentence: what is customer churn?")
            await waiter.wait()
            print(
                f"\n(+ {counters['deltas']} delta events suppressed"
                " — that flood IS the streaming)"
            )

    return 0


# Phase 3 — Tools: descriptions tell the model when to call the tool
class GetCustomerTotalParams(BaseModel):
    """Parameter schema sent to the model."""

    customer_id: Annotated[str, Field(description="Customer identifier, for example C003")]


@define_tool(description="Gets the total amount a given retail customer has spent.")
def get_customer_total(params: GetCustomerTotalParams, _invocation: ToolInvocation) -> str:
    matches = [t for t in TRANSACTIONS if t[0].casefold() == params.customer_id.casefold()]

    if not matches:
        return f"No transactions found for {params.customer_id}."

    total = sum(t[1] for t in matches)

    # Visible proof that the custom tool executed.
    print(f"  [tool] get_customer_total({params.customer_id}) -> ${total:,.2f}")
    return f"{params.customer_id} has {len(matches)} transactions totalling ${total:,.2f}."


async def phase3_tools(requested_model_id: str | None) -> int:
    print("== Phase 3: tools ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        # Run once with tools=[] to demonstrate the CLI's built-in tools.
        # TYPE THIS LIVE (Phase 3 only): replace it with tools=[get_customer_total].
        # Python tool calls require on_permission_request; approve_all is silent.
        # tools adds custom tools but does not remove built-ins.
        session = await client.create_session(
            model=model_id,
            streaming=False,
            tools=[],
            on_permission_request=PermissionHandler.approve_all,
        )

        async with session:
            waiter = IdleWaiter()

            def on_event(evt: SessionEvent) -> None:
                if evt.type is SessionEventType.ASSISTANT_MESSAGE:
                    # Tool-request messages have no displayable content.
                    if evt.data.content:
                        print(f"\nAssistant: {flatten(evt.data.content)}")
                elif evt.type is SessionEventType.SESSION_ERROR:
                    print(f"\nERROR: {evt.data.message}")
                waiter.handle(evt)

            session.on(on_event)

            # Let the model select the tool without naming it in the prompt.
            print("Prompt: How much has customer C003 spent in total?\n")
            await session.send("How much has customer C003 spent in total?")
            await waiter.wait()

    return 0


# Phase 4 — Persistence: close, then resume
async def phase4_persistence(requested_model_id: str | None) -> int:
    import uuid

    print("== Phase 4: persistence ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        # The session ID enables resume after closing.
        session_id = f"codealong-{uuid.uuid4().hex}"[:24]
        print(f"Session id: {session_id}\n")

        print("--- Turn 1 (new session) ---")
        session = await client.create_session(
            session_id=session_id, model=model_id, streaming=False
        )
        async with session:
            await send_and_print(
                session,
                "Remember this: my favourite retail segment is 'At Risk'. Reply with just OK.",
            )

        print("\nSession closed.\n")

        print("--- Turn 2 (resumed) ---")
        resumed = await client.resume_session(session_id, model=model_id, streaming=False)
        async with resumed:
            await send_and_print(resumed, "Which retail segment did I say was my favourite?")
    return 0


# Phase 5 — MCP: attach tools from a server
# Network dependent: use the recording if learn.microsoft.com is blocked.
async def phase5_mcp(requested_model_id: str | None) -> int:
    print("== Phase 5: MCP ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        session = await client.create_session(
            model=model_id,
            streaming=False,
            mcp_servers={
                "microsoft.docs.mcp": {
                    "type": "http",
                    "url": "https://learn.microsoft.com/api/mcp",
                    # Python requires this to expose the MCP server's tools.
                    "tools": ["*"],
                }
            },
            on_permission_request=PermissionHandler.approve_all,
        )

        async with session:
            waiter = IdleWaiter()

            def on_event(evt: SessionEvent) -> None:
                if evt.type is SessionEventType.TOOL_EXECUTION_START:
                    # Show whether each call came from MCP or a built-in tool.
                    server = getattr(evt.data, "mcp_server_name", None)
                    if server:
                        print(f"  [mcp] {server} :: {evt.data.mcp_tool_name}")
                    else:
                        print(f"  [built-in] {getattr(evt.data, 'tool_name', '?')}")
                elif evt.type is SessionEventType.ASSISTANT_MESSAGE:
                    if evt.data.content:
                        print(f"\nAssistant: {flatten(evt.data.content)}")
                elif evt.type is SessionEventType.SESSION_ERROR:
                    print(f"\nERROR: {evt.data.message}")
                waiter.handle(evt)

            session.on(on_event)

            # Explicit steering prevents a built-in web fetch from hiding the MCP call.
            print("Prompt: Search Microsoft Learn — what is Azure Container Apps?\n")
            await session.send(
                "Use the microsoft.docs.mcp tools to search Microsoft Learn, then tell me "
                "in two sentences what Azure Container Apps is."
            )
            await waiter.wait()

    return 0


# Dispatch
PHASES = {
    1: phase1_hello_world,
    2: phase2_events,
    3: phase3_tools,
    4: phase4_persistence,
    5: phase5_mcp,
}


async def run(phase: int, requested_model_id: str | None) -> int:
    handler = PHASES.get(phase)
    if handler is None:
        print(
            "Usage:\n"
            "  uv run python -m sdk_labs codealong --phase <1-5>\n\n"
            "  1  Hello world     — client + session, streaming\n"
            "  2  Events          — what the session emits, in order\n"
            "  3  Tools           ⭐ define a tool the model calls\n"
            "  4  Persistence     — close, then resume the same session\n"
            "  5  MCP             — attach a server you didn't write (needs network)"
        )
        return 1
    return await handler(requested_model_id)
