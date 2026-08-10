using System.ComponentModel;
using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// Lab 03 — define a tool the model can call.
///
/// Instead of pasting retail context into a system message and hoping the model
/// uses it, register a real function. The model decides when to call it.
/// </summary>
public static class ToolsSample
{
    /// <summary>In-memory stand-in for the transaction store.</summary>
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
        Console.WriteLine($"  [tool] GetCustomerTotal({customerId}) -> {total:C}");
        return $"{customerId} has {matches.Length} transactions totalling {total:C}.";
    }

    public static async Task<int> RunAsync(string? requestedModelId)
    {
        Console.WriteLine("== Lab 03: tools ==\n");

        await using var client = new CopilotClient();
        await client.StartAsync();

        var modelId = await ModelPicker.PickAsync(client, requestedModelId);
        if (modelId is null)
        {
            return 1;
        }

        var totalTool = CopilotTool.DefineTool(GetCustomerTotal);

        var config = new SessionConfig
        {
            Model = modelId,
            Streaming = false,
            Tools = [totalTool]
        };

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource();

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
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

        Console.WriteLine("Prompt: How much has customer C003 spent in total?\n");
        await session.SendAsync(new MessageOptions
        {
            Prompt = "How much has customer C003 spent in total? Use the available tool."
        });

        // Never wait forever: a dropped transport means idle/error may never arrive.
        await done.Task.WaitAsync(TimeSpan.FromMinutes(3));
        return 0;
    }
}
