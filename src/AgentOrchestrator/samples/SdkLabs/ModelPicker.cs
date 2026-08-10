using GitHub.Copilot;

namespace SdkLabs;

/// <summary>
/// Chooses a model from the caller's account instead of assuming every reader
/// has access to the same preview model.
/// </summary>
public static class ModelPicker
{
    private static readonly string[] PreferredModelIds =
    [
        "claude-haiku-4.5",
        "gpt-5-mini",
        "gpt-5.4-mini",
        "gpt-5",
        "gpt-4.1"
    ];

    public static async Task<string?> PickAsync(CopilotClient client, string? requestedModelId)
    {
        IReadOnlyList<ModelInfo> models;

        try
        {
            models = [.. await client.ListModelsAsync()];
        }
        catch (Exception ex) when (!string.IsNullOrWhiteSpace(requestedModelId))
        {
            Console.WriteLine(
                $"Could not list available models to validate '{requestedModelId}': {ex.Message}");
            Console.WriteLine($"Using requested model without validation: {requestedModelId}");
            Console.WriteLine($"Model: {requestedModelId}");
            return requestedModelId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not list available models: {ex.Message}");
            Console.WriteLine("Pass --model <id> to choose a model explicitly, or try again later.");
            return null;
        }

        if (models.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(requestedModelId))
            {
                Console.WriteLine("No available models were returned; using the requested model without validation.");
                Console.WriteLine($"Model: {requestedModelId}");
                return requestedModelId;
            }

            Console.WriteLine("No available models were returned for this account.");
            Console.WriteLine("Pass --model <id> to choose a model explicitly, or check your Copilot access.");
            return null;
        }

        var available = models
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(requestedModelId))
        {
            var requested = available.FirstOrDefault(model =>
                string.Equals(model.Id, requestedModelId, StringComparison.OrdinalIgnoreCase));

            if (requested is not null)
            {
                Console.WriteLine($"Model: {requested.Id}");
                return requested.Id;
            }
        }

        var selected = PreferredModelIds
            .Select(preferred => available.FirstOrDefault(model =>
                string.Equals(model.Id, preferred, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(model => model is not null)
            ?? available.FirstOrDefault(model =>
                !string.Equals(model.Id, "auto", StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            Console.WriteLine("Only the automatic model selector was returned; choose a concrete model with --model <id>.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(requestedModelId))
        {
            Console.WriteLine($"Requested model '{requestedModelId}' is unavailable; using '{selected.Id}' instead.");
        }
        else if (!string.Equals(selected.Id, PreferredModelIds[0], StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Preferred model '{PreferredModelIds[0]}' is unavailable; using '{selected.Id}' instead.");
        }

        Console.WriteLine($"Model: {selected.Id}");
        return selected.Id;
    }
}
