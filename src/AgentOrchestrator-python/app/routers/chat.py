"""API router for Copilot chat interactions.

Demonstrates GitHub Copilot SDK integration with streaming SSE responses.
Mirrors ``AgentHQDemo.Api/Controllers/ChatController.cs``.
"""

import json
import logging
from collections.abc import AsyncIterator

from fastapi import APIRouter, Request
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel

from app.log_sanitizer import sanitize_for_log
from app.services.copilot_chat import DEFAULT_MODEL, CopilotChatService

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/chat", tags=["chat"])


class ModelInfo(BaseModel):
    """Information about an available model."""

    id: str
    name: str
    description: str


class ChatRequest(BaseModel):
    """Request model for chat endpoints."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    prompt: str | None = None
    model: str | None = None
    system_message: str | None = None


class ChatResponse(BaseModel):
    """Response model for the non-streaming chat endpoint."""

    content: str | None = None
    model: str | None = None


#: Available models in the GitHub Copilot SDK. Used as a fallback when the CLI
#: cannot be queried, and to enrich live results with friendly descriptions.
AVAILABLE_MODELS: dict[str, ModelInfo] = {
    "claude-haiku-4.5": ModelInfo(
        id="claude-haiku-4.5",
        name="Claude Haiku 4.5",
        description="Anthropic's fastest model — great for quick responses",
    ),
    "gpt-4.1": ModelInfo(
        id="gpt-4.1", name="GPT-4.1", description="Fast and efficient for everyday coding tasks"
    ),
    "gpt-5": ModelInfo(
        id="gpt-5", name="GPT-5", description="OpenAI's most capable model for complex tasks"
    ),
    "claude-sonnet-4.5": ModelInfo(
        id="claude-sonnet-4.5",
        name="Claude Sonnet 4.5",
        description="Anthropic's balanced model for code and reasoning",
    ),
    "claude-opus-4.5": ModelInfo(
        id="claude-opus-4.5",
        name="Claude Opus 4.5",
        description="Anthropic's most powerful model for deep analysis",
    ),
    "gemini-2.5-pro": ModelInfo(
        id="gemini-2.5-pro",
        name="Gemini 2.5 Pro",
        description="Google's advanced model with large context window",
    ),
}


def _service(request: Request) -> CopilotChatService:
    return request.app.state.chat_service


@router.get("/models", response_model=list[ModelInfo])
async def get_models(request: Request) -> list[ModelInfo]:
    """Gets the list of available models.

    Queries the Copilot CLI so the picker reflects the models the signed-in
    account can actually use. Falls back to the static catalog if unavailable.
    """
    try:
        live = await _service(request).list_models()
        if live:
            return [
                AVAILABLE_MODELS.get(
                    model_id,
                    ModelInfo(id=model_id, name=name, description="Available via GitHub Copilot"),
                )
                for model_id, name in live
            ]
    except Exception:
        # Includes the known SDK issue where ModelBilling is missing the
        # required 'multiplier' field: github/copilot-sdk#1302.
        logger.warning(
            "Could not list models from Copilot CLI; using static catalog", exc_info=True
        )

    return list(AVAILABLE_MODELS.values())


@router.post("/stream")
async def stream_chat(request: Request, body: ChatRequest) -> StreamingResponse:
    """Sends a chat message and streams the response using Server-Sent Events (SSE).

    This endpoint demonstrates real-time streaming of AI responses, similar to
    ChatGPT's typing effect.
    """
    model = body.model or DEFAULT_MODEL
    prompt = body.prompt or ""
    logger.info(
        "Starting chat stream with model %s for prompt: %s",
        sanitize_for_log(model),
        sanitize_for_log(prompt[:50]),
    )

    service = _service(request)

    async def event_stream() -> AsyncIterator[str]:
        try:
            async for chunk in service.chat_stream(prompt, model, body.system_message):
                if await request.is_disconnected():
                    break
                yield f"data: {json.dumps({'content': chunk})}\n\n"

            yield "data: [DONE]\n\n"
        except Exception:  # noqa: BLE001 - reported to the client as an SSE frame
            logger.exception("Error during chat stream")
            yield f"data: {json.dumps({'error': 'An error occurred during the chat stream.'})}\n\n"

    return StreamingResponse(
        event_stream(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "Connection": "keep-alive"},
    )


@router.post("", response_model=ChatResponse)
async def chat(request: Request, body: ChatRequest) -> ChatResponse:
    """Sends a chat message and returns the complete response."""
    model = body.model or DEFAULT_MODEL
    logger.info("Processing chat request with model %s", sanitize_for_log(model))

    response = await _service(request).chat(body.prompt or "", model, body.system_message)
    return ChatResponse(content=response, model=model)


@router.get("/health")
async def health() -> dict:
    """Health check for the chat service."""
    return {
        "status": "healthy",
        "service": "CopilotChat",
        "availableModels": list(AVAILABLE_MODELS.keys()),
    }
