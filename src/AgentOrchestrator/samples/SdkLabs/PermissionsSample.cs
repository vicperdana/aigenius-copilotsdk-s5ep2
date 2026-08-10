using System.ComponentModel;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace SdkLabs;

/// <summary>
/// Lab 05 — approve or deny tool calls in-process.
///
/// The SDK-native counterpart to a shell hook: instead of an external script
/// vetting commands, your own C# decides, with the full request in hand.
/// </summary>
public static class PermissionsSample
{
    [Description("Deletes a customer record permanently.")]
    private static string DeleteCustomer(
        [Description("Customer identifier, for example C003")] string customerId)
    {
        // Never actually reached in this sample — the handler denies it.
        return $"Deleted {customerId}.";
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
        Console.WriteLine("== Lab 05: permissions ==\n");

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

        await done.Task;
        return 0;
    }
}
