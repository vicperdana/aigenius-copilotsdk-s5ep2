"""AI Genius S5E2 — LIVE CODE-ALONG (starter).

Run one phase at a time::

    uv run python -m sdk_labs codealong --phase 1

Phase 0 and the plumbing in each phase are DONE FOR YOU on purpose — the boring
parts are pre-written so the only thing that happens on camera is the part that
teaches something.

Look for:   # TYPE THIS LIVE
Everything else is scaffolding. Fall back to ``code_along_final`` at any time.
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

# ── Phase 0 — done for you ──────────────────────────────────────────────────
# An in-memory stand-in for the transaction store, so nothing depends on the
# database being seeded.
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


# ════════════════════════════════════════════════════════════════════════════
# PHASE 1 — Hello World: a client and a session
#
# Talk track: "Think of it like a phone. The CLIENT is dialling the number.
# The SESSION is the actual conversation once someone picks up."
# ════════════════════════════════════════════════════════════════════════════
async def phase1_hello_world(requested_model_id: str | None) -> int:
    print("== Phase 1: hello world ==\n")

    # Done for you: start the client and pick a model this account can use.
    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        # TYPE THIS LIVE ─────────────────────────────────────────────────────
        # "Now the conversation. streaming=True means we get tokens as they're
        #  produced rather than waiting for the whole answer."
        session = await client.create_session(model=model_id, streaming=True)
        # ─────────────────────────────────────────────────────────────────────

        async with session:
            # Done for you: print deltas as they arrive, stop when idle.
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


# ════════════════════════════════════════════════════════════════════════════
# PHASE 2 — Events: what the session actually emits
#
# Talk track: "Observe and iterate are the whole ballgame. This is the SDK
# telling you what it's doing, in order. Nothing hidden."
# ════════════════════════════════════════════════════════════════════════════
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

            # TYPE THIS LIVE ─────────────────────────────────────────────────
            # "Instead of handling specific events, print the type of every
            #  event that arrives. Deltas flood, so count those instead."
            def on_event(evt: SessionEvent) -> None:
                nonlocal finished
                name = evt.type.value

                # Shutdown events keep arriving after idle — stop printing so
                # the summary line stays last on screen.
                if finished:
                    return

                # Every *delta* event arrives in a flood — that IS the
                # streaming. Count them so the lifecycle stays readable.
                if "delta" in name.lower():
                    counters["deltas"] += 1
                    return

                counters["order"] += 1
                print(f"{counters['order']:3d}. {name}")

                if evt.type is SessionEventType.SESSION_IDLE:
                    finished = True

                waiter.handle(evt)

            session.on(on_event)
            # ─────────────────────────────────────────────────────────────────

            await session.send("In one sentence: what is customer churn?")
            await waiter.wait()
            print(
                f"\n(+ {counters['deltas']} delta events suppressed"
                " — that flood IS the streaming)"
            )

    return 0


# ════════════════════════════════════════════════════════════════════════════
# PHASE 3 — Tools  ⭐ THE CENTREPIECE
#
# Talk track: "Up to now I've been hoping the model knows things. Now I'm going
# to hand it one of my own functions and let it decide when to call it."
#
# In Python the parameter schema is a Pydantic model, and the FIELD
# DESCRIPTIONS are what the model actually sees. That text is the contract.
# ════════════════════════════════════════════════════════════════════════════

# TYPE THIS LIVE — the params model + the decorator ──────────────────────────
class GetCustomerTotalParams(BaseModel):
    """Parameter schema sent to the model."""

    customer_id: Annotated[str, Field(description="Customer identifier, for example C003")]


@define_tool(description="Gets the total amount a given retail customer has spent.")
def get_customer_total(params: GetCustomerTotalParams, _invocation: ToolInvocation) -> str:
    # Body is done for you — it's just a sum, not the interesting part.
    matches = [t for t in TRANSACTIONS if t[0].casefold() == params.customer_id.casefold()]

    if not matches:
        return f"No transactions found for {params.customer_id}."

    total = sum(t[1] for t in matches)

    # This line is your PROOF on stage that YOUR code ran.
    print(f"  [tool] get_customer_total({params.customer_id}) -> ${total:,.2f}")
    return f"{params.customer_id} has {len(matches)} transactions totalling ${total:,.2f}."
# ─────────────────────────────────────────────────────────────────────────────


async def phase3_tools(requested_model_id: str | None) -> int:
    print("== Phase 3: tools ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        # ⚠️ RUN THIS ONCE *BEFORE* YOU TYPE ANYTHING — it's the best beat in
        # the whole session. With tools=[] the model still answers, because the
        # host CLI's built-in file and shell tools let it go and rummage
        # through your repo. Verified live: it found the SQLite database and
        # started querying the schema.
        #
        # Say: "I never gave it a tool. It went and read my filesystem. That's
        #       the default posture — capable, and permissive. Now watch what
        #       happens when I hand it exactly one function I control."
        #
        # Then add the tool below and run it again.

        # TYPE THIS LIVE ─────────────────────────────────────────────────────
        # Put get_customer_total inside the empty tools list below.
        #
        # NOTE — unlike .NET, Python REQUIRES on_permission_request. Leave it
        # out and the tool call is denied and the model reports a permission
        # error instead of an answer. That's a gift: it puts the governance
        # conversation on screen without a separate slide.
        session = await client.create_session(
            model=model_id,
            streaming=False,
            tools=[],
            on_permission_request=PermissionHandler.approve_all,
        )
        # ─────────────────────────────────────────────────────────────────────

        async with session:
            waiter = IdleWaiter()

            def on_event(evt: SessionEvent) -> None:
                if evt.type is SessionEventType.ASSISTANT_MESSAGE:
                    # The first assistant message only carries the tool
                    # request, so its content is empty — skip it.
                    if evt.data.content:
                        print(f"\nAssistant: {flatten(evt.data.content)}")
                elif evt.type is SessionEventType.SESSION_ERROR:
                    print(f"\nERROR: {evt.data.message}")
                waiter.handle(evt)

            session.on(on_event)

            # Never name the tool in the prompt on stage. Letting the model
            # choose it unprompted is the entire point.
            print("Prompt: How much has customer C003 spent in total?\n")
            await session.send("How much has customer C003 spent in total?")
            await waiter.wait()

    return 0


# ════════════════════════════════════════════════════════════════════════════
# PHASE 4 — Persistence: kill it, bring it back
#
# Talk track: "Without this, every restart is amnesia. With it, the
# conversation has an identity you can pick up anywhere."
# ════════════════════════════════════════════════════════════════════════════
async def phase4_persistence(requested_model_id: str | None) -> int:
    import uuid

    print("== Phase 4: persistence ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        # TYPE THIS LIVE — the id is the whole trick ─────────────────────────
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

        # Say out loud: "That session object is now GONE. Closed."
        print("\nSession closed.\n")

        print("--- Turn 2 (resumed) ---")
        resumed = await client.resume_session(session_id, model=model_id, streaming=False)
        async with resumed:
            await send_and_print(resumed, "Which retail segment did I say was my favourite?")
        # ─────────────────────────────────────────────────────────────────────

    return 0


# ════════════════════════════════════════════════════════════════════════════
# PHASE 5 — MCP: tools you didn't write
#
# ⚠️ NETWORK DEPENDENT. This reaches learn.microsoft.com. If the venue blocks
# it, play the recording instead — do not debug this on stage.
# ════════════════════════════════════════════════════════════════════════════
async def phase5_mcp(requested_model_id: str | None) -> int:
    print("== Phase 5: MCP ==\n")

    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1

        # TYPE THIS LIVE — the mcp_servers block is the payload ──────────────
        # "In phase three I wrote the tool. Here I write no tool at all — I
        #  point at a server someone else runs, and my agent gains its toolset."
        session = await client.create_session(
            model=model_id,
            streaming=False,
            mcp_servers={
                "microsoft.docs.mcp": {
                    "type": "http",
                    "url": "https://learn.microsoft.com/api/mcp",
                    # ⚠️ Python REQUIRES this. Omit "tools" and the server
                    # loads but exposes nothing — the model reports it has no
                    # access and quietly falls back to a built-in web fetch.
                    # .NET does not need an equivalent.
                    "tools": ["*"],
                }
            },
            on_permission_request=PermissionHandler.approve_all,
        )
        # ─────────────────────────────────────────────────────────────────────

        async with session:
            waiter = IdleWaiter()

            def on_event(evt: SessionEvent) -> None:
                if evt.type is SessionEventType.TOOL_EXECUTION_START:
                    # Print EVERY tool call, and mark the ones that came from
                    # MCP. Without this, a run where the model picks a built-in
                    # (web_fetch) instead looks identical to nothing happening.
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

            # Steer explicitly to the MCP server. Left vague, the model may
            # reach for a built-in web fetch instead and you lose the proof.
            print("Prompt: Search Microsoft Learn — what is Azure Container Apps?\n")
            await session.send(
                "Use the microsoft.docs.mcp tools to search Microsoft Learn, then tell me "
                "in two sentences what Azure Container Apps is."
            )
            await waiter.wait()

    return 0


# ── Dispatch — done for you ─────────────────────────────────────────────────
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
