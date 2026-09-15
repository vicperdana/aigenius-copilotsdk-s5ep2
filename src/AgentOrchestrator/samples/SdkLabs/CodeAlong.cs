using System.ComponentModel;
using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// AI Genius S5E2 — LIVE CODE-ALONG (starter)
///
/// Run one phase at a time:
///     dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- codealong --phase 1
///
/// Only the Phase 3 block marked TYPE THIS LIVE is edited on stage.
/// Fall back to CodeAlongFinal at any time.
/// </summary>
public static class CodeAlong
{
    // Phase 0 — in-memory demo data
    private static readonly (string CustomerId, decimal Amount, string Category)[] Transactions =
    [
        ("C001", 245.50m, "Grocery"),
        ("C001", 89.99m, "Electronics"),
        ("C002", 32.00m, "Grocery"),
        ("C002", 15.50m, "Health"),
        ("C003", 1250.00m, "Electronics"),
        ("C003", 450.00m, "Fashion"),
        ("C004", 12.99m, "Grocery"),
        ("C004", 8.50m, "Grocery"),
        ("C005", 675.00m, "Electronics"),
        ("C005", 320.00m, "Fashion")
    ];

    public static async Task<int> RunAsync(int phase, string? requestedModelId) => phase switch
    {
        1 => await Phase1HelloWorldAsync(requestedModelId),
        2 => await Phase2EventsAsync(requestedModelId),
        3 => await Phase3ToolsAsync(requestedModelId),
        4 => await Phase4PersistenceAsync(requestedModelId),
        5 => await Phase5McpAsync(requestedModelId),
        _ => PhaseUsage()
    };

    // Phase 1 — Hello World: a client and a session
    private static async Task<int> Phase1HelloWorldAsync(string? requestedModelId)
    {
        Console.WriteLine("== Phase 1: hello world ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        // Streaming emits token deltas as they arrive.
        var config = new SessionConfig
        {
            Model = modelId,
            Streaming = true
        };

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource();

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    Console.Write(delta.Data.DeltaContent ?? "");
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent error:
                    Console.WriteLine($"\nERROR: {error.Data.Message}");
                    done.TrySetException(new Exception(error.Data.Message));
                    break;
            }
        });

        Console.WriteLine("Prompt: Name three retail KPIs. One line each.\n");
        await session.SendAsync(new MessageOptions
        {
            Prompt = "Name three retail KPIs. One line each."
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        Console.WriteLine();
        return 0;
    }

    // Phase 2 — Events: what the session emits
    private static async Task<int> Phase2EventsAsync(string? requestedModelId)
    {
        Console.WriteLine("== Phase 2: events ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = modelId,
            Streaming = true
        });

        var done = new TaskCompletionSource();
        var order = 0;
        var deltaCount = 0;

        // Count streaming deltas so the lifecycle remains readable.
        session.On<SessionEvent>(evt =>
        {
            var name = evt.GetType().Name;

            if (name.EndsWith("DeltaEvent", StringComparison.Ordinal))
            {
                deltaCount++;
                return;
            }

            // Generic envelopes do not represent a lifecycle step.
            if (name is nameof(SessionEvent))
            {
                return;
            }

            Console.WriteLine($"{++order,3}. {name}");

            if (evt is SessionIdleEvent)
            {
                done.TrySetResult();
            }
        });
        await session.SendAsync(new MessageOptions
        {
            Prompt = "In one sentence: what is customer churn?"
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        Console.WriteLine($"\n(+ {deltaCount} *DeltaEvent suppressed — that flood IS the streaming)");
        return 0;
    }

    // Phase 3 — Tools: descriptions tell the model when to call the tool
    [Description("Gets the total amount a given retail customer has spent.")]
    private static string GetCustomerTotal(
        [Description("Customer identifier, for example C003")] string customerId)
    {
        var matches = Transactions.Where(t =>
            string.Equals(t.CustomerId, customerId, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (matches.Length == 0)
        {
            return $"No transactions found for {customerId}.";
        }

        var total = matches.Sum(t => t.Amount);

        // Visible proof that the custom tool executed.
        Console.WriteLine($"  [tool] GetCustomerTotal({customerId}) -> {total:C}");
        return $"{customerId} has {matches.Length} transactions totalling {total:C}.";
    }

    private static async Task<int> Phase3ToolsAsync(string? requestedModelId)
    {
        Console.WriteLine("== Phase 3: tools ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        // Run once with Tools = [] to demonstrate the CLI's built-in tools.
        // ✍️ TYPE THIS LIVE (Phase 3 only):
        //     var totalTool = CopilotTool.DefineTool(GetCustomerTotal);
        // Then replace Tools = [] with Tools = [totalTool].

        var config = new SessionConfig
        {
            Model = modelId,
            Streaming = false,
            Tools = []
        };

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource();

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg when !string.IsNullOrWhiteSpace(msg.Data.Content):
                    Console.WriteLine($"\nAssistant: {msg.Data.Content}");
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent error:
                    Console.WriteLine($"\nERROR: {error.Data.Message}");
                    done.TrySetException(new Exception(error.Data.Message));
                    break;
            }
        });

        // Let the model select the tool without naming it in the prompt.
        Console.WriteLine("Prompt: How much has customer C003 spent in total?\n");
        await session.SendAsync(new MessageOptions
        {
            Prompt = "How much has customer C003 spent in total?"
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        return 0;
    }

    // Phase 4 — Persistence: dispose, then resume
    private static async Task<int> Phase4PersistenceAsync(string? requestedModelId)
    {
        Console.WriteLine("== Phase 4: persistence ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        // The session ID enables resume after disposal.
        var sessionId = $"codealong-{Guid.NewGuid():N}"[..24];
        Console.WriteLine($"Session id: {sessionId}\n");

        Console.WriteLine("--- Turn 1 (new session) ---");
        await using (var session = await client.CreateSessionAsync(new SessionConfig
        {
            SessionId = sessionId,
            Model = modelId,
            Streaming = false
        }))
        {
            await SendAndPrintAsync(session,
                "Remember this: my favourite retail segment is 'At Risk'. Reply with just OK.");
        }

        Console.WriteLine("\nSession disposed.\n");

        Console.WriteLine("--- Turn 2 (resumed) ---");
        await using (var resumed = await client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
        {
            Model = modelId,
            Streaming = false
        }))
        {
            await SendAndPrintAsync(resumed,
                "Which retail segment did I say was my favourite?");
        }
        return 0;
    }

    // Phase 5 — MCP: attach tools from a server
    // Network dependent: use the recording if learn.microsoft.com is blocked.
    private static async Task<int> Phase5McpAsync(string? requestedModelId)
    {
        Console.WriteLine("== Phase 5: MCP ==\n");

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

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case ToolExecutionStartEvent start:
                    // Show whether each call came from MCP or a built-in tool.
                    Console.WriteLine(!string.IsNullOrWhiteSpace(start.Data.McpServerName)
                        ? $"  [mcp] {start.Data.McpServerName} :: {start.Data.McpToolName}"
                        : $"  [built-in] {start.Data.ToolName}");
                    break;
                case AssistantMessageEvent msg when !string.IsNullOrWhiteSpace(msg.Data.Content):
                    Console.WriteLine($"\nAssistant: {msg.Data.Content}");
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent error:
                    Console.WriteLine($"\nERROR: {error.Data.Message}");
                    done.TrySetException(new Exception(error.Data.Message));
                    break;
            }
        });

        Console.WriteLine("Prompt: Search Microsoft Learn — what is Azure Container Apps?\n");
        await session.SendAsync(new MessageOptions
        {
            // Explicit steering prevents a built-in web fetch from hiding the MCP call.
            Prompt = "Use the microsoft.docs.mcp tools to search Microsoft Learn, then tell me "
                   + "in two sentences what Azure Container Apps is."
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        return 0;
    }

    // Helpers
    private static async Task SendAndPrintAsync(CopilotSession session, string prompt)
    {
        var done = new TaskCompletionSource();

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg when !string.IsNullOrWhiteSpace(msg.Data.Content):
                    Console.WriteLine($"Assistant: {msg.Data.Content}");
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

        Console.WriteLine($"You: {prompt}");
        await session.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
    }

    private static int PhaseUsage()
    {
        Console.WriteLine("""
            Usage:
              dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- codealong --phase <1-5>

              1  Hello world     — client + session, streaming
              2  Events          — what the session emits, in order
              3  Tools           ⭐ define a tool the model calls
              4  Persistence     — dispose, then resume the same session
              5  MCP             — attach a server you didn't write (needs network)
            """);
        return 1;
    }
}
