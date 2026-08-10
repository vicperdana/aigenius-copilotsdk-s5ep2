using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// Lab 06 — persist and resume a session.
///
/// Creates a session with a known id, sends a message, disposes it, then
/// resumes the *same* session in a fresh call and checks the agent still has
/// the earlier context. This is the "your agent, anywhere" building block.
/// </summary>
public static class SessionsSample
{
    public static async Task<int> RunAsync()
    {
        Console.WriteLine("== Lab 06: sessions ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var sessionId = $"sdklabs-{Guid.NewGuid():N}"[..24];
        Console.WriteLine($"Session id: {sessionId}\n");

        // --- First conversation turn -------------------------------------
        Console.WriteLine("--- Turn 1 (new session) ---");
        await using (var session = await client.CreateSessionAsync(new SessionConfig
        {
            SessionId = sessionId,
            Model = "claude-haiku-4.5",
            Streaming = false
        }))
        {
            await SendAndPrintAsync(session,
                "Remember this: my favourite retail segment is 'At Risk'. Reply with just OK.");
        }

        Console.WriteLine("\nSession disposed.\n");

        // --- Resume the same session -------------------------------------
        Console.WriteLine("--- Turn 2 (resumed session) ---");
        await using (var resumed = await client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
        {
            Model = "claude-haiku-4.5",
            Streaming = false
        }))
        {
            await SendAndPrintAsync(resumed,
                "Which retail segment did I say was my favourite?");
        }

        // --- Inspect stored sessions -------------------------------------
        Console.WriteLine("\n--- Session metadata ---");
        var metadata = await client.GetSessionMetadataAsync(sessionId);
        Console.WriteLine(metadata is null
            ? "  (no metadata returned)"
            : $"  id={sessionId} metadata retrieved");

        return 0;
    }

    private static async Task SendAndPrintAsync(CopilotSession session, string prompt)
    {
        var done = new TaskCompletionSource();

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg when !string.IsNullOrWhiteSpace(msg.Data.Content):
                    Console.WriteLine($"Assistant: {msg.Data.Content.ReplaceLineEndings(" ")}");
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
        await done.Task;
    }
}
