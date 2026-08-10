using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// Lab 04 — observe the session event lifecycle.
///
/// Logs every event the session emits, in order, so you can see what actually
/// arrives and in what sequence rather than guessing from the type names.
/// </summary>
public static class EventsSample
{
    public static async Task<int> RunAsync()
    {
        Console.WriteLine("== Lab 04: events ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var config = new SessionConfig
        {
            Model = "claude-haiku-4.5",
            Streaming = true
        };

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource();
        var order = 0;
        var deltaCount = 0;

        session.On<SessionEvent>(evt =>
        {
            // Deltas arrive in a flood; count them instead of printing each one.
            if (evt is AssistantMessageDeltaEvent)
            {
                deltaCount++;
                return;
            }

            Console.WriteLine($"{++order,3}. {evt.GetType().Name}");

            switch (evt)
            {
                case AssistantMessageEvent msg:
                    Console.WriteLine($"     content: {Trim(msg.Data.Content)}");
                    Console.WriteLine($"     (preceded by {deltaCount} delta events)");
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent error:
                    Console.WriteLine($"     ERROR: {error.Data.Message}");
                    done.TrySetException(new Exception(error.Data.Message));
                    break;
            }
        });

        Console.WriteLine("Prompt: Name two retail KPIs. One line each.\n");
        await session.SendAsync(new MessageOptions
        {
            Prompt = "Name two retail KPIs. One line each."
        });

        // Never wait forever: a dropped transport means idle/error may never arrive.
        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));

        Console.WriteLine($"\nTotal delta events: {deltaCount}");
        return 0;
    }

    private static string Trim(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "(empty)";
        var flat = s.ReplaceLineEndings(" ");
        return flat.Length <= 80 ? flat : flat[..80] + "…";
    }
}
