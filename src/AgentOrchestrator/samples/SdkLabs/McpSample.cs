using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// Lab 06 — attach an MCP server.
///
/// Uses the Microsoft Learn MCP server over HTTP — the same one this repo
/// already configures for VS Code in .vscode/mcp.json — so there is nothing
/// extra to install.
/// </summary>
public static class McpSample
{
    public static async Task<int> RunAsync(string? requestedModelId)
    {
        Console.WriteLine("== Lab 06: mcp ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        var config = new SessionConfig
        {
            Model = modelId,
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

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource();
        var traceEvents = string.Equals(
            Environment.GetEnvironmentVariable("SDKLABS_TRACE_EVENTS"),
            "1",
            StringComparison.OrdinalIgnoreCase);
        var invokedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mcpTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolCallNames = new Dictionary<string, string>();

        session.On<SessionEvent>(evt =>
        {
            if (traceEvents)
            {
                Console.WriteLine($"  [event] {evt.GetType().Name}");
            }

            switch (evt)
            {
                // Surface whether the configured server actually connected —
                // without this, a failed server is indistinguishable from a
                // model that simply chose not to call a tool.
                case SessionMcpServersLoadedEvent:
                case SessionMcpServerStatusChangedEvent:
                case McpToolsListChangedEvent:
                    Console.WriteLine($"  [mcp] {evt.GetType().Name}");
                    break;
                case ToolExecutionStartEvent start:
                    var toolName = FormatToolName(
                        start.Data.ToolName,
                        start.Data.McpServerName,
                        start.Data.McpToolName);
                    if (string.IsNullOrWhiteSpace(toolName))
                    {
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(start.Data.ToolCallId))
                    {
                        toolCallNames[start.Data.ToolCallId] = toolName;
                    }

                    // Only a tool carrying an MCP server name came from MCP.
                    // Built-ins such as web_fetch must not count, or an
                    // unreachable server still reports success.
                    if (!string.IsNullOrWhiteSpace(start.Data.McpServerName))
                    {
                        mcpTools.Add(toolName);
                    }

                    LogToolInvocation(invokedTools, evt.GetType().Name, toolName);
                    break;
                case ToolExecutionCompleteEvent complete
                    when !string.IsNullOrWhiteSpace(complete.Data.ToolCallId)
                      && toolCallNames.TryGetValue(complete.Data.ToolCallId, out var completedToolName):
                    Console.WriteLine(
                        $"  [tool] {evt.GetType().Name}: {completedToolName} (success={complete.Data.Success})");
                    break;
                case ExternalToolRequestedEvent request
                    when !string.IsNullOrWhiteSpace(request.Data.ToolName):
                    Console.WriteLine($"  [tool-request] {evt.GetType().Name}: {request.Data.ToolName}");
                    break;
                case AssistantMessageEvent msg when !string.IsNullOrWhiteSpace(msg.Data.Content):
                    Console.WriteLine($"\nAssistant: {msg.Data.Content.ReplaceLineEndings(" ")}");
                    break;
                case AssistantMessageEvent msg:
                    LogToolRequests(evt.GetType().Name, msg);
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent error:
                    Console.WriteLine($"ERROR: {error.Data.Message}");
                    done.TrySetException(new Exception(error.Data.Message));
                    break;
            }
        });

        Console.WriteLine("Prompt: asking the model to consult Microsoft Learn docs\n");
        await session.SendAsync(new MessageOptions
        {
            Prompt = "Using the Microsoft Learn tools available to you, "
                   + "what is Azure Container Apps? Answer in two sentences."
        });

        // Never wait forever: a dropped transport means idle/error may never arrive.
        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));

        if (mcpTools.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("⚠️  No MCP tool was invoked.");
            if (invokedTools.Count > 0)
            {
                Console.WriteLine(
                    $"    The model used non-MCP tool(s) instead: {string.Join(", ", invokedTools)}");
            }
            Console.WriteLine(
                "    The answer may have come from the model's own knowledge or a built-in");
            Console.WriteLine(
                "    tool rather than Microsoft Learn. Check the server is reachable and that");
            Console.WriteLine(
                "    its tools were loaded — look for the [mcp] lines above.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"✅ MCP tool(s) invoked: {string.Join(", ", mcpTools)}");
        return 0;
    }

    private static void LogToolRequests(string eventName, AssistantMessageEvent msg)
    {
        if (msg.Data.ToolRequests is null)
        {
            return;
        }

        foreach (var toolRequest in msg.Data.ToolRequests)
        {
            var toolName = FormatToolName(
                toolRequest.Name,
                toolRequest.McpServerName,
                toolRequest.McpToolName);

            if (!string.IsNullOrWhiteSpace(toolName))
            {
                Console.WriteLine($"  [tool-request] {eventName}: {toolName}");
            }
        }
    }

    private static string FormatToolName(string? name, string? mcpServerName = null, string? mcpToolName = null)
    {
        if (!string.IsNullOrWhiteSpace(mcpServerName) && !string.IsNullOrWhiteSpace(mcpToolName))
        {
            return $"{mcpServerName}/{mcpToolName}";
        }

        return name ?? "";
    }

    private static void LogToolInvocation(HashSet<string> invokedTools, string eventName, string toolName)
    {
        invokedTools.Add(toolName);
        Console.WriteLine($"  [tool] {eventName}: {toolName}");
    }
}
