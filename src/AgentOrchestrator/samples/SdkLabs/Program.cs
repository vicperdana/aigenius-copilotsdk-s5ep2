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
            "sessions" => await SessionsSample.RunAsync(),
            "mcp" => await McpSample.RunAsync(),
            "permissions" => await PermissionsSample.RunAsync(),
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
              sessions      Lab 05 — persist and resume a session
              mcp           Lab 06 — attach an MCP server

            Diagnostic (no lab):
              permissions   Reference code for SessionConfig.OnPermissionRequest.
                            The handler was NOT observed firing here: the host CLI
                            pre-approves tool use, and even a reject-everything
                            handler still let the command run. Treat this as a
                            starting point to test in your own environment, not as
                            a working security control. See docs/labs/03-tools/.
            """);
        return 1;
    }
}
