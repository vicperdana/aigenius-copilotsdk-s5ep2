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
        var options = ParseOptions(args.Skip(1).ToArray());

        if (options is null)
        {
            return Usage();
        }

        return command switch
        {
            "tools" => await ToolsSample.RunAsync(options.ModelId),
            "events" => await EventsSample.RunAsync(options.ModelId),
            "sessions" => await SessionsSample.RunAsync(options.ModelId, options.ResumeSessionId),
            "mcp" => await McpSample.RunAsync(options.ModelId),
            "permissions" => await PermissionsSample.RunAsync(options.ModelId),
            _ => Usage()
        };
    }

    private static SampleOptions? ParseOptions(string[] args)
    {
        string? modelId = null;
        string? resumeSessionId = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--model" when i + 1 < args.Length:
                    modelId = args[++i];
                    break;
                case "--resume" when i + 1 < args.Length:
                    resumeSessionId = args[++i];
                    break;
                default:
                    Console.WriteLine($"Unknown or incomplete option: {args[i]}");
                    return null;
            }
        }

        return new SampleOptions(modelId, resumeSessionId);
    }

    private static int Usage()
    {
        Console.WriteLine("""
            SdkLabs — runnable samples for the Copilot SDK labs.

            Usage:
              dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- <command> [--model <id>]

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

    private sealed record SampleOptions(string? ModelId, string? ResumeSessionId);
}
