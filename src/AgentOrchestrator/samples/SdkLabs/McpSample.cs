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
    public static async Task<int> RunAsync()
    {
        Console.WriteLine("== Lab 06: mcp ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var config = new SessionConfig
        {
            Model = "claude-haiku-4.5",
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

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg when !string.IsNullOrWhiteSpace(msg.Data.Content):
                    Console.WriteLine($"\nAssistant: {msg.Data.Content.ReplaceLineEndings(" ")}");
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
        return 0;
    }
}
