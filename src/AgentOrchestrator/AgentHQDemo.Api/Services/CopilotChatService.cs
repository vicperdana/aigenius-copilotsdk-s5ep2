using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace AgentHQDemo.Api.Services;

/// <summary>
/// Service for managing Copilot chat sessions.
/// Demonstrates GitHub Copilot SDK integration for the Three Mondays demo.
/// </summary>
/// <remarks>
/// The session attaches the read-only retail MCP server (AgentHQDemo.McpServer),
/// so the model can answer questions from real data instead of guessing.
/// See <see cref="BuildRetailMcpServers"/> and <see cref="HandlePermissionRequest"/>.
/// </remarks>
public class CopilotChatService : IAsyncDisposable
{
    /// <summary>
    /// Name the session registers the retail MCP server under. The permission
    /// handler below only trusts this name.
    /// </summary>
    private const string RetailMcpServer = "retail-analytics";

    private readonly ILogger<CopilotChatService> _logger;
    private CopilotClient? _client;
    private bool _isStarted;

    public CopilotChatService(ILogger<CopilotChatService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds the stdio config for the read-only retail MCP server.
    /// </summary>
    /// <remarks>
    /// Returns null when the database has not been created yet, so the chat
    /// still works (just without data access) instead of failing to start.
    /// </remarks>
    private Dictionary<string, McpServerConfig>? BuildRetailMcpServers()
    {
        var apiDirectory = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(apiDirectory, "retail.db")))
        {
            _logger.LogWarning(
                "retail.db not found in {Directory} — starting chat without database access",
                apiDirectory);
            return null;
        }

        var serverDll = ResolveMcpServerPath();
        if (serverDll is null)
        {
            _logger.LogWarning(
                "MCP server not built — run dotnet build on AgentHQDemo.slnx. "
                + "Starting chat without database access.");
            return null;
        }

        return new Dictionary<string, McpServerConfig>
        {
            [RetailMcpServer] = new McpStdioServerConfig
            {
                Command = "dotnet",
                Args = [serverDll],
                WorkingDirectory = apiDirectory
            }
        };
    }

    /// <summary>
    /// Finds the MCP server's build output.
    /// </summary>
    /// <remarks>
    /// Derived from this assembly's own location, so the configuration and
    /// target framework always match the running API. Probing a hardcoded
    /// "Debug" first would launch a stale Debug build when the API itself is
    /// running in Release.
    /// </remarks>
    private static string? ResolveMcpServerPath()
    {
        const string dllName = "AgentHQDemo.McpServer.dll";

        // .../AgentHQDemo.Api/bin/<Configuration>/<Tfm>/ -> .../AgentHQDemo.McpServer/bin/<Configuration>/<Tfm>/
        var apiOutput = AppContext.BaseDirectory;
        var sibling = apiOutput.Replace(
            $"{Path.DirectorySeparatorChar}AgentHQDemo.Api{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}AgentHQDemo.McpServer{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);

        var candidate = Path.Combine(sibling, dllName);
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        // Fall back to a source-tree layout, matching the running configuration
        // first so a stale build of the other one is never preferred.
        var configuration = new DirectoryInfo(apiOutput).Parent?.Name;
        var configurations = configuration is null
            ? ["Debug", "Release"]
            : new[] { configuration }.Concat(new[] { "Debug", "Release" }).Distinct();

        var tfm = new DirectoryInfo(apiOutput).Name;

        return configurations
            .Select(config => Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "AgentHQDemo.McpServer", "bin", config, tfm,
                dllName)))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Approves only read-only tools from our own MCP server.
    /// </summary>
    /// <remarks>
    /// ⚠️ Defence in depth, not the primary control. As documented in
    /// samples/SdkLabs/PermissionsSample.cs, this hook was never observed
    /// firing when the host Copilot CLI has pre-granted tool approval. The
    /// guarantee that actually holds is the MCP server's read-only SQLite
    /// connection, which rejects writes at the driver.
    /// </remarks>
    private Task<PermissionDecision> HandlePermissionRequest(
        PermissionRequest request,
        PermissionInvocation invocation)
    {
        if (request is PermissionRequestMcp mcp
            && mcp.ServerName == RetailMcpServer
            && mcp.ReadOnly)
        {
            return Task.FromResult(PermissionDecision.ApproveOnce());
        }

        _logger.LogWarning("Denied permission request: {Kind}", request.Kind);
        return Task.FromResult(PermissionDecision.Reject(
            "Only read-only retail-analytics tools are permitted in this chat."));
    }

    /// <summary>
    /// Ensures the Copilot client is started. Recreates if connection was lost.
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        if (_isStarted && _client != null) return;

        // Reset in case of a previous failed connection
        _isStarted = false;
        if (_client != null)
        {
            try { await _client.StopAsync(); } catch { /* ignore cleanup errors */ }
        }

        _client = new CopilotClient();
        await _client.StartAsync();
        _isStarted = true;
        _logger.LogInformation("Copilot client started");
    }

    /// <summary>
    /// Lists the models the connected Copilot CLI actually offers.
    /// </summary>
    public async Task<IReadOnlyList<(string Id, string Name)>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync();

        if (_client == null) return [];

        var models = await _client.ListModelsAsync(cancellationToken);
        return models?
            .Where(m => !string.IsNullOrWhiteSpace(m.Id))
            .Select(m => (m.Id!, string.IsNullOrWhiteSpace(m.Name) ? m.Id! : m.Name!))
            .ToList() ?? [];
    }

    /// <summary>
    /// Sends a chat message and streams the response.
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string prompt,
        string model = "claude-haiku-4.5",
        string? systemMessage = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync();

        if (_client == null)
        {
            yield return "Error: Copilot client not initialized";
            yield break;
        }

        _logger.LogInformation("Creating session with model: {Model}", LogSanitizer.Sanitize(model));

        var outputChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        // Run the Copilot session in a background task so we can yield outside try/catch
        _ = Task.Run(async () =>
        {
            try
            {
                SessionConfig config = new()
                {
                    Model = model,
                    Streaming = true,
                    McpServers = BuildRetailMcpServers(),
                    OnPermissionRequest = HandlePermissionRequest,
                    SystemMessage = systemMessage != null ? new SystemMessageConfig
                    {
                        Mode = SystemMessageMode.Append,
                        Content = systemMessage
                    } : null
                };

                await using var session = await _client.CreateSessionAsync(config);
                var done = new TaskCompletionSource();

                session.On<SessionEvent>(evt =>
                {
                    switch (evt)
                    {
                        case AssistantMessageDeltaEvent delta:
                            outputChannel.Writer.TryWrite(delta.Data.DeltaContent ?? "");
                            break;
                        case AssistantMessageEvent msg:
                            _logger.LogInformation("Assistant response complete: {Length} chars", msg.Data.Content?.Length ?? 0);
                            break;
                        case ToolExecutionStartEvent start when !string.IsNullOrWhiteSpace(start.Data.McpServerName):
                            // Makes MCP visible in the API logs, which is what
                            // the lab asks you to watch for.
                            _logger.LogInformation("MCP tool call: {Server}/{Tool}",
                                start.Data.McpServerName, start.Data.McpToolName);
                            break;
                        case SessionIdleEvent:
                            done.SetResult();
                            break;
                        case SessionErrorEvent error:
                            _logger.LogError("Session error: {Message}", error.Data.Message);
                            done.SetException(new Exception(error.Data.Message));
                            break;
                    }
                });

                await session.SendAsync(new MessageOptions { Prompt = prompt });
                await done.Task;
                outputChannel.Writer.Complete();
            }
            catch (IOException ex)
            {
                _logger.LogWarning("Copilot connection lost: {Message}", ex.Message);
                _isStarted = false;
                outputChannel.Writer.Complete(ex);
            }
            catch (Exception ex)
            {
                outputChannel.Writer.Complete(ex);
            }
        }, cancellationToken);

        await foreach (var chunk in outputChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Sends a chat message and returns the complete response.
    /// </summary>
    public async Task<string> ChatAsync(
        string prompt, 
        string model = "claude-haiku-4.5",
        string? systemMessage = null, 
        CancellationToken cancellationToken = default)
    {
        var response = new System.Text.StringBuilder();
        await foreach (var chunk in ChatStreamAsync(prompt, model, systemMessage, cancellationToken))
        {
            response.Append(chunk);
        }
        return response.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            _logger.LogInformation("Copilot client stopped");
        }
    }
}
