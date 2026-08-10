using System.ComponentModel;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace SdkLabs;

/// <summary>
/// Diagnostic — NOT a lab. Reference code for
/// <c>SessionConfig.OnPermissionRequest</c>.
///
/// ⚠️ This handler was never observed firing. Even a version that rejected
/// every request still let the shell command run, because the host Copilot CLI
/// had pre-granted tool approval. Keep this as a starting point for testing
/// whether the hook engages in YOUR environment — do not treat it as a working
/// security control. See docs/labs/03-tools/ for the full caveat, and
/// docs/labs/extra-governance-hooks/ for the shell-hook alternative.
/// </summary>
public static class PermissionsSample
{
    [Description("Deletes a customer record permanently.")]
    private static string DeleteCustomer(
        [Description("Customer identifier, for example C003")] string customerId)
    {
        // Deliberately inert — this sample must never mutate anything.
        Console.WriteLine($"  [tool] DeleteCustomer({customerId}) — no-op in this sample");
        return $"Pretended to delete {customerId}.";
    }

    [Description("Gets the number of transactions for a customer.")]
    private static string CountTransactions(
        [Description("Customer identifier, for example C003")] string customerId)
    {
        Console.WriteLine($"  [tool] CountTransactions({customerId}) -> 2");
        return $"{customerId} has 2 transactions.";
    }

    public static async Task<int> RunAsync()
    {
        Console.WriteLine("== Diagnostic: permissions (not a lab) ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var config = new SessionConfig
        {
            Model = "claude-haiku-4.5",
            Streaming = false,
            Tools =
            [
                CopilotTool.DefineTool(CountTransactions),
                CopilotTool.DefineTool(DeleteCustomer)
            ],
            OnPermissionRequest = (request, invocation) =>
            {
                Console.WriteLine($"  [permission] requested: kind={request.Kind}");

                // Policy: allow reads, refuse anything destructive.
                var kind = request.Kind?.ToString() ?? "";
                if (kind.Contains("delete", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("  [permission] -> REJECTED by policy");
                    return Task.FromResult(PermissionDecision.Reject(
                        "Destructive operations are not permitted in this demo."));
                }

                Console.WriteLine("  [permission] -> approved once");
                return Task.FromResult(PermissionDecision.ApproveOnce());
            }
        };

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource();

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case PermissionRequestedEvent req:
                    Console.WriteLine($"  [permission] requested: {req.Data?.GetType().Name}");
                    break;
                case PermissionCompletedEvent done2:
                    Console.WriteLine($"  [permission] result: {done2.Data?.Result?.GetType().Name}");
                    break;
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

        Console.WriteLine("Prompt: asking the model to run a shell command\n");
        await session.SendAsync(new MessageOptions
        {
            Prompt = "Run the shell command `echo hello-from-lab-05` and tell me its output."
        });

        // Never wait forever: a dropped transport means idle/error may never arrive.
        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        return 0;
    }
}
