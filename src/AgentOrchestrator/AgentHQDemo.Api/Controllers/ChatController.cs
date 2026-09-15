using Microsoft.AspNetCore.Mvc;
using AgentHQDemo.Api.Logging;
using AgentHQDemo.Api.Services;
using System.Text.Json;

namespace AgentHQDemo.Api.Controllers;

/// <summary>
/// API controller for Copilot chat interactions.
/// Demonstrates GitHub Copilot SDK integration with streaming SSE responses.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly CopilotChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    /// <summary>
    /// What the client is told when a chat call fails.
    /// </summary>
    /// <remarks>
    /// Exception text can carry file paths, connection strings, or SDK internals,
    /// so the detail stays in the server log and the caller gets a fixed string.
    /// Mirrors <c>_CLIENT_ERROR_MESSAGE</c> in <c>app/routers/chat.py</c>.
    /// </remarks>
    private const string ClientErrorMessage = "An error occurred while processing your request.";

    /// <summary>
    /// Available models in GitHub Copilot SDK.
    /// </summary>
    public static readonly Dictionary<string, ModelInfo> AvailableModels = new()
    {
        ["claude-haiku-4.5"] = new("claude-haiku-4.5", "Claude Haiku 4.5", "Anthropic's fastest model — great for quick responses"),
        ["gpt-4.1"] = new("gpt-4.1", "GPT-4.1", "Fast and efficient for everyday coding tasks"),
        ["gpt-5"] = new("gpt-5", "GPT-5", "OpenAI's most capable model for complex tasks"),
        ["claude-sonnet-4.5"] = new("claude-sonnet-4.5", "Claude Sonnet 4.5", "Anthropic's balanced model for code and reasoning"),
        ["claude-opus-4.5"] = new("claude-opus-4.5", "Claude Opus 4.5", "Anthropic's most powerful model for deep analysis"),
        ["gemini-2.5-pro"] = new("gemini-2.5-pro", "Gemini 2.5 Pro", "Google's advanced model with large context window"),
    };

    public ChatController(CopilotChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Whether a model is internal-only and should be kept out of the picker.
    /// </summary>
    /// <remarks>
    /// Accounts with internal entitlements see models named like
    /// "GPT-5.6 Sol Fast (Internal only)". Showing those during a demo, stream, or
    /// screenshot leaks the account's access scope.
    /// <para>
    /// The display name is the only signal available: the SDK's ModelInfo carries
    /// just id, name, capabilities, policy (state/terms), and billing (multiplier) —
    /// there is no visibility or internal flag, and policy.state describes whether a
    /// model is enabled, not who may see it. Matching on the name is therefore
    /// deliberately brittle. If the SDK ever exposes a real visibility field, this
    /// method is the single place to change.
    /// </para>
    /// </remarks>
    public static bool IsInternalOnly(string? name) =>
        name is not null && name.Contains("internal", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the list of available models.
    /// </summary>
    /// <remarks>
    /// Queries the Copilot CLI so the picker reflects the models the signed-in
    /// account can actually use. Internal-only models are filtered out. Falls back
    /// to the static catalog if unavailable.
    /// </remarks>
    [HttpGet("models")]
    public async Task<ActionResult<IEnumerable<ModelInfo>>> GetModels(CancellationToken cancellationToken)
    {
        try
        {
            var live = await _chatService.ListModelsAsync(cancellationToken);
            var visible = live.Where(m => !IsInternalOnly(m.Name)).ToList();
            if (visible.Count > 0)
            {
                return Ok(visible.Select(m => AvailableModels.TryGetValue(m.Id, out var known)
                    ? known
                    : new ModelInfo(m.Id, m.Name, "Available via GitHub Copilot")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list models from Copilot CLI; using static catalog");
        }

        return Ok(AvailableModels.Values);
    }

    /// <summary>
    /// Sends a chat message and streams the response using Server-Sent Events (SSE).
    /// </summary>
    /// <remarks>
    /// This endpoint demonstrates real-time streaming of AI responses,
    /// similar to ChatGPT's typing effect.
    /// </remarks>
    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model ?? "claude-haiku-4.5";
        _logger.LogInformation("Starting chat stream with model {Model} for prompt: {Prompt}",
            LogSanitizer.Sanitize(model), LogSanitizer.Sanitize(request.Prompt, 50));

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var chunk in _chatService.ChatStreamAsync(
                request.Prompt ?? "",
                model,
                request.SystemMessage,
                cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;

                var data = JsonSerializer.Serialize(new { content = chunk });
                await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chat stream");
            var errorData = JsonSerializer.Serialize(new { error = ClientErrorMessage });
            await Response.WriteAsync($"data: {errorData}\n\n", cancellationToken);
        }
    }

    /// <summary>
    /// Sends a chat message and returns the complete response.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model ?? "claude-haiku-4.5";
        _logger.LogInformation("Processing chat request with model {Model}", LogSanitizer.Sanitize(model));

        try
        {
            var response = await _chatService.ChatAsync(
                request.Prompt ?? "",
                model,
                request.SystemMessage,
                cancellationToken);

            return Ok(new ChatResponse { Content = response, Model = model });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chat");
            return StatusCode(500, new { error = ClientErrorMessage });
        }
    }

    /// <summary>
    /// Health check for the chat service.
    /// </summary>
    [HttpGet("health")]
    public ActionResult<object> Health()
    {
        return Ok(new { status = "healthy", service = "CopilotChat", availableModels = AvailableModels.Keys });
    }
}

/// <summary>
/// Information about an available model.
/// </summary>
public record ModelInfo(string Id, string Name, string Description);

/// <summary>
/// Request model for chat endpoints.
/// </summary>
public record ChatRequest
{
    public string? Prompt { get; init; }
    public string? Model { get; init; }
    public string? SystemMessage { get; init; }
}

/// <summary>
/// Response model for non-streaming chat endpoint.
/// </summary>
public record ChatResponse
{
    public string? Content { get; init; }
    public string? Model { get; init; }
}
