using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// Lab 05 — persist and resume a session.
///
/// Creates a session with a known id, sends a message, disposes it, then
/// resumes the *same* session. Passing <c>--resume &lt;id&gt;</c> lets readers
/// prove the same idea from a second process when the same store is available.
/// </summary>
public static class SessionsSample
{
    public static async Task<int> RunAsync(string? requestedModelId, string? resumeSessionId)
    {
        Console.WriteLine("== Lab 05: sessions ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(resumeSessionId))
        {
            return await ResumeExistingAsync(client, resumeSessionId, modelId);
        }

        var sessionId = $"sdklabs-{Guid.NewGuid():N}"[..24];
        Console.WriteLine($"Session id: {sessionId}\n");

        // --- First conversation turn -------------------------------------
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

        // --- Resume the same session -------------------------------------
        Console.WriteLine("--- Turn 2 (resumed session) ---");
        await using (var resumed = await client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
        {
            Model = modelId,
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

    private static async Task<int> ResumeExistingAsync(
        CopilotClient client,
        string sessionId,
        string modelId)
    {
        Console.WriteLine($"Session id: {sessionId}\n");
        Console.WriteLine("--- Resumed existing session ---");

        await using (var resumed = await client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
        {
            Model = modelId,
            Streaming = false
        }))
        {
            await SendAndPrintAsync(resumed,
                "Which retail segment did I say was my favourite?");
        }

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
        // Never wait forever: a dropped transport means idle/error may never arrive.
        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
    }
}
