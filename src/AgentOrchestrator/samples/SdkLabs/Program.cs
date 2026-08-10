using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// Runnable samples backing the Copilot SDK labs in docs/labs/.
/// Each subcommand maps to one lab.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var command = args.FirstOrDefault();

        return command switch
        {
            "tools" => await ToolsSample.RunAsync(),
            "events" => await EventsSample.RunAsync(),
            "permissions" => await PermissionsSample.RunAsync(),
            "sessions" => await SessionsSample.RunAsync(),
            "mcp" => await McpSample.RunAsync(),
            _ => Usage()
        };
    }

    private static int Usage()
    {
        Console.WriteLine("""
            SdkLabs — runnable samples for the Copilot SDK labs.

            Usage:
              dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- <command>

            Commands:
              tools         Lab 03 — define a tool the model can call
              events        Lab 04 — observe the session event lifecycle
              permissions   Lab 05 — approve or deny tool calls in-process
              sessions      Lab 06 — persist and resume a session
              mcp           Lab 07 — attach an MCP server
            """);
        return 1;
    }
}
