"""Service for managing Copilot chat sessions.

Demonstrates GitHub Copilot SDK integration for the Three Mondays demo.

The interesting difference from the .NET version is streaming. The SDK delivers
events by *calling a handler*, not by exposing an async iterator, so this module
bridges those callbacks into an ``asyncio.Queue`` and drains the queue as an
async generator. .NET does the same thing with ``System.Threading.Channels``.

The session also attaches the read-only retail MCP server (``mcp_server/``), so
the model can answer questions from real data instead of guessing. See
``_retail_mcp_config`` and ``_permission_handler`` below.
"""

import asyncio
import logging
import sys
from collections.abc import AsyncIterator
from pathlib import Path
from typing import Any

from copilot import (
    CopilotClient,
    PermissionNoResult,
    PermissionRequest,
    PermissionRequestResult,
    SessionEvent,
    SessionEventType,
)
from copilot.rpc import PermissionDecisionReject
from copilot.session import PermissionDecisionApproveOnce, PermissionInvocation
from copilot.session_events import PermissionRequestMcp

from app.log_sanitizer import sanitize

logger = logging.getLogger(__name__)

DEFAULT_MODEL = "claude-haiku-4.5"

#: Name the session registers the retail MCP server under. The permission
#: handler below only trusts this name.
RETAIL_MCP_SERVER = "retail-analytics"

#: Repository root for the Python track — the working directory the MCP server
#: is launched from, and where retail.db lives.
PROJECT_ROOT = Path(__file__).resolve().parents[2]

# Sentinel pushed onto the queue when the session goes idle.
_DONE = object()


def _retail_mcp_config() -> dict[str, Any] | None:
    """Builds the stdio config for the read-only retail MCP server.

    Returns ``None`` when the database has not been created yet, so the chat
    still works (just without data access) instead of failing to start.

    ``sys.executable`` is used rather than "uv" so the server runs on the very
    same interpreter as the API, with no extra tooling required at runtime.
    """
    if not (PROJECT_ROOT / "retail.db").exists():
        logger.warning(
            "retail.db not found in %s — starting chat without database access", PROJECT_ROOT
        )
        return None

    # Note there is no "type" key: the CLI infers stdio from `command`,
    # whereas a remote server is identified by `url`. Use `working_directory`,
    # not `cwd` — the SDK renames it on the way to the wire format, and only
    # the public key is covered by the MCPStdioServerConfig TypedDict.
    return {
        RETAIL_MCP_SERVER: {
            "command": sys.executable,
            "args": ["-m", "mcp_server"],
            "working_directory": str(PROJECT_ROOT),
            "tools": ["*"],
        }
    }


def _permission_handler(
    request: PermissionRequest, invocation: PermissionInvocation
) -> PermissionRequestResult:
    """Approves only read-only tools from our own MCP server.

    The labs use ``PermissionHandler.approve_all``, which is fine for a console
    sample but too broad for a service that answers requests from a browser. A
    prompt-injected instruction to run a shell command or write a file arrives
    here as a non-MCP request and gets refused.
    """
    if invocation.get("managed_settings_enabled", False):
        return PermissionNoResult()

    if (
        isinstance(request, PermissionRequestMcp)
        and request.server_name == RETAIL_MCP_SERVER
        and request.read_only
    ):
        return PermissionDecisionApproveOnce()

    logger.warning(
        "Denied permission request: %s", getattr(request, "kind", type(request).__name__)
    )
    return PermissionDecisionReject(
        feedback="Only read-only retail-analytics tools are permitted in this chat."
    )


class CopilotChatService:
    def __init__(self) -> None:
        self._client: CopilotClient | None = None
        self._is_started = False
        self._lock = asyncio.Lock()

    async def ensure_started(self) -> None:
        """Ensures the Copilot client is started. Recreates if connection was lost."""
        async with self._lock:
            if self._is_started and self._client is not None:
                return

            # Reset in case of a previous failed connection
            self._is_started = False
            if self._client is not None:
                try:
                    await self._client.stop()
                except Exception:  # noqa: BLE001 - ignore cleanup errors
                    pass

            self._client = CopilotClient()
            await self._client.start()
            self._is_started = True
            logger.info("Copilot client started")

    async def list_models(self) -> list[tuple[str, str]]:
        """Lists the models the connected Copilot CLI actually offers."""
        await self.ensure_started()

        if self._client is None:
            return []

        models = await self._client.list_models()
        return [(m.id, m.name or m.id) for m in models or [] if m.id]

    async def chat_stream(
        self,
        prompt: str,
        model: str = DEFAULT_MODEL,
        system_message: str | None = None,
    ) -> AsyncIterator[str]:
        """Sends a chat message and streams the response."""
        await self.ensure_started()

        if self._client is None:
            yield "Error: Copilot client not initialized"
            return

        logger.info("Creating session with model: %s", sanitize(model))

        queue: asyncio.Queue[object] = asyncio.Queue()
        loop = asyncio.get_running_loop()

        async def run_session() -> None:
            try:
                mcp_servers = _retail_mcp_config()
                session = await self._client.create_session(
                    model=model,
                    streaming=True,
                    system_message=(
                        {"mode": "append", "content": system_message} if system_message else None
                    ),
                    mcp_servers=mcp_servers,
                    # Required in Python: without a handler the SDK denies every
                    # tool call, so the MCP server would load but never be used.
                    on_permission_request=_permission_handler,
                )

                async with session:
                    done: asyncio.Future[None] = loop.create_future()

                    def on_event(evt: SessionEvent) -> None:
                        # Unlike .NET, every event arrives as one SessionEvent
                        # carrying a `type` enum and a `data` payload, so this
                        # dispatches on `evt.type` rather than on subclasses.
                        if evt.type is SessionEventType.ASSISTANT_MESSAGE_DELTA:
                            queue.put_nowait(evt.data.delta_content or "")
                        elif evt.type is SessionEventType.ASSISTANT_MESSAGE:
                            logger.info(
                                "Assistant response complete: %d chars",
                                len(evt.data.content or ""),
                            )
                        elif evt.type is SessionEventType.TOOL_EXECUTION_START:
                            # Makes MCP visible in the API logs, which is what
                            # the lab asks you to watch for.
                            if evt.data.mcp_server_name:
                                logger.info(
                                    "MCP tool call: %s/%s",
                                    evt.data.mcp_server_name,
                                    evt.data.mcp_tool_name,
                                )
                        elif evt.type is SessionEventType.SESSION_IDLE:
                            if not done.done():
                                done.set_result(None)
                        elif evt.type is SessionEventType.SESSION_ERROR:
                            logger.error("Session error: %s", evt.data.message)
                            if not done.done():
                                done.set_exception(RuntimeError(evt.data.message))

                    session.on(on_event)

                    await session.send(prompt)
                    await done

                queue.put_nowait(_DONE)
            except (ConnectionError, OSError) as ex:
                logger.warning("Copilot connection lost: %s", ex)
                self._is_started = False
                queue.put_nowait(ex)
            except Exception as ex:  # noqa: BLE001 - surfaced to the caller below
                queue.put_nowait(ex)

        task = asyncio.create_task(run_session())

        try:
            while True:
                item = await queue.get()
                if item is _DONE:
                    break
                if isinstance(item, BaseException):
                    raise item
                yield item  # type: ignore[misc]
        finally:
            if not task.done():
                task.cancel()

    async def chat(
        self,
        prompt: str,
        model: str = DEFAULT_MODEL,
        system_message: str | None = None,
    ) -> str:
        """Sends a chat message and returns the complete response."""
        chunks = [chunk async for chunk in self.chat_stream(prompt, model, system_message)]
        return "".join(chunks)

    async def close(self) -> None:
        if self._client is not None:
            await self._client.stop()
            self._client = None
            self._is_started = False
            logger.info("Copilot client stopped")
