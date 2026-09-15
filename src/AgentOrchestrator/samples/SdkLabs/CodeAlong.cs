using System.ComponentModel;
using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// AI Genius S5E2 — LIVE CODE-ALONG (starter)
///
/// Run one phase at a time:
///     dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- codealong --phase 1
///
/// Phases 0 and the plumbing in each phase are DONE FOR YOU on purpose — the
/// boring parts are pre-written so the only thing that happens on camera is the
/// part that teaches something.
///
/// Two kinds of marker, and the difference matters on camera:
///
///     // ✍️ TYPE THIS LIVE   you genuinely type this. Phase 3 only.
///     // 👆 WALK THROUGH     already written. Highlight it and narrate it.
///
/// Everything else is scaffolding. Fall back to CodeAlongFinal at any time.
/// </summary>
public static class CodeAlong
{
    // ── Phase 0 — done for you ──────────────────────────────────────────────
    // An in-memory stand-in for the transaction store, so nothing depends on
    // the database being seeded.
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

    // ════════════════════════════════════════════════════════════════════════
    // PHASE 1 — Hello World: a client and a session
    //
    // Talk track: "Think of it like a phone. The CLIENT is dialling the number.
    // The SESSION is the actual conversation once someone picks up."
    // ════════════════════════════════════════════════════════════════════════
    private static async Task<int> Phase1HelloWorldAsync(string? requestedModelId)
    {
        Console.WriteLine("== Phase 1: hello world ==\n");

        // Done for you: start the client and pick a model that this account can use.
        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        // 👆 WALK THROUGH — already written, highlight and narrate ───────────
        // "Now the conversation. Streaming true means we get tokens as they're
        //  produced rather than waiting for the whole answer."
        var config = new SessionConfig
        {
            Model = modelId,
            Streaming = true
        };

        await using var session = await client.CreateSessionAsync(config);
        // ─────────────────────────────────────────────────────────────────────

        // Done for you: print deltas as they arrive, stop when the session goes idle.
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

    // ════════════════════════════════════════════════════════════════════════
    // PHASE 2 — Events: what the session actually emits
    //
    // Talk track: "Observe and iterate are the whole ballgame. This is the SDK
    // telling you what it's doing, in order. Nothing hidden."
    // ════════════════════════════════════════════════════════════════════════
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

        // 👆 WALK THROUGH — already written, highlight and narrate ───────────
        // "Instead of handling specific events, print the type name of every
        //  event that arrives. Deltas flood, so count those instead."
        session.On<SessionEvent>(evt =>
        {
            var name = evt.GetType().Name;

            // Every *Delta* event arrives in a flood — that IS the streaming.
            // Count them so the lifecycle stays readable on a projector.
            if (name.EndsWith("DeltaEvent", StringComparison.Ordinal))
            {
                deltaCount++;
                return;
            }

            // The SDK also emits base-typed SessionEvent envelopes we have no
            // specific handling for. Skip them — they're noise on stage.
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
        // ─────────────────────────────────────────────────────────────────────

        await session.SendAsync(new MessageOptions
        {
            Prompt = "In one sentence: what is customer churn?"
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        Console.WriteLine($"\n(+ {deltaCount} *DeltaEvent suppressed — that flood IS the streaming)");
        return 0;
    }

    // ════════════════════════════════════════════════════════════════════════
    // PHASE 3 — Tools  ⭐ THE CENTREPIECE
    //
    // Talk track: "Up to now I've been hoping the model knows things. Now I'm
    // going to hand it one of my own functions and let it decide when to call it."
    //
    // The [Description] text is not a comment. It is the contract — it is
    // literally how the model knows what this does and when to reach for it.
    // ════════════════════════════════════════════════════════════════════════

    // 👆 WALK THROUGH — already written. THIS is the concept, so narrate it ──
    // The [Description] attributes are not comments. They are the contract:
    // that plain English is how the model knows what this method does and when
    // to reach for it. Read them out loud. Do not retype the method on camera.
    [Description("Gets the total amount a given retail customer has spent.")]
    private static string GetCustomerTotal(
        [Description("Customer identifier, for example C003")] string customerId)
    {
        // Body is done for you — it's just a LINQ sum, not the interesting part.
        var matches = Transactions.Where(t =>
            string.Equals(t.CustomerId, customerId, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (matches.Length == 0)
        {
            return $"No transactions found for {customerId}.";
        }

        var total = matches.Sum(t => t.Amount);

        // This line is your PROOF on stage that YOUR code ran.
        Console.WriteLine($"  [tool] GetCustomerTotal({customerId}) -> {total:C}");
        return $"{customerId} has {matches.Length} transactions totalling {total:C}.";
    }
    // ─────────────────────────────────────────────────────────────────────────

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

        // ⚠️ RUN THIS ONCE *BEFORE* YOU TYPE ANYTHING — it's the best beat in
        // the whole session. With Tools = [] the model still answers, because
        // the host CLI's built-in file and shell tools let it go and rummage
        // through your repo. Verified live: it found the SQLite database and
        // started querying the schema.
        //
        // Say: "I never gave it a tool. It went and read my filesystem. That's
        //       the default posture — capable, and permissive. Now watch what
        //       happens when I hand it exactly one function I control."
        //
        // Then type the two lines below and run it again.

        // ✍️ TYPE THIS LIVE — the only thing you type all session ────────────
        // "Two lines. Wrap the method as a tool, then hand it to the session."
        //
        //     var totalTool = CopilotTool.DefineTool(GetCustomerTotal);
        //
        // ...then put totalTool inside the empty Tools list below.

        var config = new SessionConfig
        {
            Model = modelId,
            Streaming = false,
            Tools = []
        };
        // ─────────────────────────────────────────────────────────────────────

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

        // Note: never name the tool in the prompt on stage. Letting the model
        // choose it unprompted is the entire point.
        Console.WriteLine("Prompt: How much has customer C003 spent in total?\n");
        await session.SendAsync(new MessageOptions
        {
            Prompt = "How much has customer C003 spent in total?"
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        return 0;
    }

    // ════════════════════════════════════════════════════════════════════════
    // PHASE 4 — Persistence: kill it, bring it back
    //
    // Talk track: "Without this, every restart is amnesia. With it, the
    // conversation has an identity you can pick up anywhere."
    // ════════════════════════════════════════════════════════════════════════
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

        // 👆 WALK THROUGH — already written. The id is the whole trick ───────
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

        // Say out loud: "That session object is now GONE. Disposed."
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
        // ─────────────────────────────────────────────────────────────────────

        return 0;
    }

    // ════════════════════════════════════════════════════════════════════════
    // PHASE 5 — MCP: tools you didn't write
    //
    // ⚠️ NETWORK DEPENDENT. This reaches learn.microsoft.com. If the venue
    // blocks it, play the recording instead — do not debug this on stage.
    // ════════════════════════════════════════════════════════════════════════
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

        // 👆 WALK THROUGH — already written. The McpServers block is the payload
        // "In phase three I wrote the tool. Here I write no tool at all — I
        //  point at a server someone else runs, and my agent gains its toolset."
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
        // ─────────────────────────────────────────────────────────────────────

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource();

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case ToolExecutionStartEvent start:
                    // Print EVERY tool call, and mark the ones that came from
                    // MCP. Without this, a run where the model picks a built-in
                    // (web_fetch) instead looks identical to nothing happening.
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
            // Steer explicitly to the MCP server. Left vague, the model may
            // reach for a built-in web fetch instead and you lose the proof.
            Prompt = "Use the microsoft.docs.mcp tools to search Microsoft Learn, then tell me "
                   + "in two sentences what Azure Container Apps is."
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        return 0;
    }

    // ── Helpers — done for you ──────────────────────────────────────────────
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
